using System.Net;
using System.Net.Http.Json;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class TaskQueryIntegrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Pages_have_stable_order_correct_totals_and_no_overlapping_items()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Pages");
        await SeedTasks(projectId, Enumerable.Range(1, 5)
            .Select(i => ($"Task {i}", TaskItemStatus.ToDo)).ToArray());
        var root = $"/api/projects/{projectId}/tasks";
        var first = await ReadJson(await client.GetAsync($"{root}?pageNumber=1&pageSize=2"), HttpStatusCode.OK);
        var second = await ReadJson(await client.GetAsync($"{root}?pageNumber=2&pageSize=2"), HttpStatusCode.OK);
        var last = await ReadJson(await client.GetAsync($"{root}?pageNumber=3&pageSize=2"), HttpStatusCode.OK);
        Assert.Equal(5, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, first.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
        Assert.False(first.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(first.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(2, second.GetProperty("items").GetArrayLength());
        Assert.Single(last.GetProperty("items").EnumerateArray());
        Assert.True(last.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(last.GetProperty("hasNextPage").GetBoolean());
        var ids = new[] { first, second, last }.SelectMany(page => page.GetProperty("items").EnumerateArray())
            .Select(task => task.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(5, ids.Distinct().Count());
        var repeated = await ReadJson(await client.GetAsync($"{root}?pageSize=2"), HttpStatusCode.OK);
        Assert.Equal(first.GetProperty("items").GetRawText(), repeated.GetProperty("items").GetRawText());
        var beyond = await ReadJson(await client.GetAsync($"{root}?pageNumber=2147483647&pageSize=1"), HttpStatusCode.OK);
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
        Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());
    }

    [PostgreSqlFact]
    public async Task Default_page_size_limits_results_and_maximum_size_is_supported()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Default page");
        await SeedTasks(projectId, Enumerable.Range(1, 21)
            .Select(i => ($"Task {i}", TaskItemStatus.ToDo)).ToArray());
        var root = $"/api/projects/{projectId}/tasks";
        var page = await ReadJson(await client.GetAsync(root), HttpStatusCode.OK);
        Assert.Equal(20, page.GetProperty("items").GetArrayLength());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(21, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        var maximum = await ReadJson(await client.GetAsync($"{root}?pageSize=100"), HttpStatusCode.OK);
        Assert.Equal(21, maximum.GetProperty("items").GetArrayLength());
    }

    [PostgreSqlFact]
    public async Task Status_and_title_filters_combine_before_counting_and_paging_with_project_isolation()
    {
        using var factory = database.CreateFactory();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        using var anonymous = factory.CreateClient();
        await RegisterAndLogin(owner);
        await RegisterAndLogin(other);
        var projectId = await CreateProject(owner, "Filtered");
        var sibling = await CreateProject(owner, "Sibling");
        var foreign = await CreateProject(other, "Foreign");
        await SeedTasks(projectId, ("Alpha one", TaskItemStatus.ToDo), ("ALPHA two", TaskItemStatus.ToDo),
            ("alpha done", TaskItemStatus.Done), ("beta", TaskItemStatus.ToDo), ("In progress", TaskItemStatus.InProgress));
        await SeedTasks(sibling, ("Alpha sibling", TaskItemStatus.ToDo));
        await SeedTasks(foreign, ("Alpha foreign", TaskItemStatus.ToDo));
        var root = $"/api/projects/{projectId}/tasks";
        var query = "status=0&search=%20aLpHa%20&pageSize=1";
        var first = await ReadJson(await owner.GetAsync($"{root}?{query}"), HttpStatusCode.OK);
        var second = await ReadJson(await owner.GetAsync($"{root}?{query}&pageNumber=2"), HttpStatusCode.OK);
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        foreach (var page in new[] { first, second })
        {
            var task = Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.Equal(projectId, task.GetProperty("projectId").GetGuid());
            Assert.Equal(0, task.GetProperty("status").GetInt32());
            Assert.Contains("alpha", task.GetProperty("title").GetString()!, StringComparison.OrdinalIgnoreCase);
        }
        var done = await ReadJson(await owner.GetAsync($"{root}?status=2"), HttpStatusCode.OK);
        Assert.Equal(1, done.GetProperty("totalCount").GetInt32());
        var inProgress = await ReadJson(await owner.GetAsync($"{root}?status=1"), HttpStatusCode.OK);
        Assert.Equal(1, inProgress.GetProperty("totalCount").GetInt32());
        await AssertProblem(await other.GetAsync($"{root}?{query}"), HttpStatusCode.NotFound);
        using var denied = await anonymous.GetAsync($"{root}?{query}");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Search_treats_sql_wildcards_as_literal_characters_and_empty_results_keep_metadata()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Literal search");
        await SeedTasks(projectId, ("100% complete", TaskItemStatus.ToDo), ("100X complete", TaskItemStatus.ToDo),
            ("file_name", TaskItemStatus.ToDo), ("fileXname", TaskItemStatus.ToDo),
            (@"C:\work", TaskItemStatus.ToDo), ("C:/work", TaskItemStatus.ToDo));
        var root = $"/api/projects/{projectId}/tasks";
        foreach (var search in new[] { "%", "_", "\\" })
        {
            var page = await ReadJson(await client.GetAsync($"{root}?search={Uri.EscapeDataString(search)}"), HttpStatusCode.OK);
            var task = Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.Contains(search, task.GetProperty("title").GetString());
            Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        }
        var empty = await ReadJson(await client.GetAsync($"{root}?search=missing"), HttpStatusCode.OK);
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, empty.GetProperty("totalPages").GetInt32());
        Assert.False(empty.GetProperty("hasNextPage").GetBoolean());
        var whitespace = await ReadJson(await client.GetAsync($"{root}?search=%20%20"), HttpStatusCode.OK);
        Assert.Equal(6, whitespace.GetProperty("totalCount").GetInt32());
    }

    [PostgreSqlFact]
    public async Task Invalid_query_parameters_return_readable_400_instead_of_overflow_or_unbounded_pages()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Query validation");
        var root = $"/api/projects/{projectId}/tasks";
        foreach (var query in new[]
        {
            "pageNumber=0", "pageNumber=-1", "pageNumber=text", "pageNumber=2147483648",
            "pageSize=0", "pageSize=-1", "pageSize=101", "pageSize=text",
            "pageNumber=2147483647&pageSize=100", "status=999", "status=-1", "status=missing",
            $"search={new string('x', 151)}"
        })
        {
            await AssertValidation(await client.GetAsync($"{root}?{query}"));
        }
    }

    [PostgreSqlFact]
    public async Task ADMIN_role_does_not_bypass_project_ownership_checks()
    {
        using var factory = database.CreateFactory();
        using var admin = factory.CreateClient();
        using var owner = factory.CreateClient();
        var email = await RegisterAndLogin(admin);
        await RegisterAndLogin(owner);
        var projectId = await CreateProject(owner, "Still private");
        var taskId = await CreateTask(owner, projectId, "Still private task");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AdminRoleProvisioner>().GrantAsync(email);
        }
        using var login = await admin.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ReadJson(await admin.GetAsync("/api/admin/users"), HttpStatusCode.OK);
        await AssertProblem(await admin.GetAsync($"/api/projects/{projectId}"), HttpStatusCode.NotFound);
        await AssertProblem(await admin.GetAsync($"/api/projects/{projectId}/tasks?status=0&search=private"), HttpStatusCode.NotFound);
        await AssertProblem(await admin.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{taskId}", new { title = "Stolen" }), HttpStatusCode.NotFound);
    }

    private async Task SeedTasks(Guid projectId, params (string Title, TaskItemStatus Status)[] tasks)
    {
        await using var context = database.CreateContext();
        context.TaskItems.AddRange(tasks.Select(task => new TaskItem
        {
            ProjectId = projectId, Title = task.Title, Status = task.Status
        }));
        await context.SaveChangesAsync();
    }
}
