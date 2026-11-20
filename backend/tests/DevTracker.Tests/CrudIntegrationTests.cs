using System.Net;
using static DevTracker.Tests.ApiTestClient;
using System.Net.Http.Json;
using System.Text.Json;
using DevTracker.Application.Exceptions;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class CrudIntegrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{

    [PostgreSqlFact]
    public async Task Projects_support_all_five_operations_and_partial_updates()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "CRUD project", "original description");

        var list = await ReadJson(await client.GetAsync("/api/projects"), HttpStatusCode.OK);
        Assert.Contains(list.GetProperty("projects").EnumerateArray(), p => p.GetProperty("id").GetGuid() == projectId);
        var path = $"/api/projects/{projectId}";
        var project = (await ReadJson(await client.GetAsync(path), HttpStatusCode.OK)).GetProperty("project");
        Assert.Equal("CRUD project", project.GetProperty("name").GetString());
        Assert.Equal(0, project.GetProperty("status").GetInt32());
        Assert.NotEqual(default, project.GetProperty("createdAt").GetDateTimeOffset());

        await ReadJson(await client.PatchAsJsonAsync(path, new { projectStatus = 1 }), HttpStatusCode.OK);
        project = (await ReadJson(await client.GetAsync(path), HttpStatusCode.OK)).GetProperty("project");
        Assert.Equal(1, project.GetProperty("status").GetInt32());
        Assert.Equal("CRUD project", project.GetProperty("name").GetString());
        Assert.Equal("original description", project.GetProperty("description").GetString());

        var updated = await ReadJson(await client.PatchAsJsonAsync(path,
            new { name = "Renamed", description = (string?)null }), HttpStatusCode.OK);
        Assert.Equal("Renamed", updated.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("description").ValueKind);
        project = (await ReadJson(await client.GetAsync(path), HttpStatusCode.OK)).GetProperty("project");
        Assert.Equal("Renamed", project.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, project.GetProperty("description").ValueKind);
        Assert.True(project.GetProperty("updatedAt").GetDateTimeOffset() >= project.GetProperty("createdAt").GetDateTimeOffset());

        using var deleted = await client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());
        await AssertProblem(await client.GetAsync(path), HttpStatusCode.NotFound);
        await AssertProblem(await client.DeleteAsync(path), HttpStatusCode.NotFound);
        await AssertProblem(await client.PatchAsJsonAsync(path, new { name = "Missing" }), HttpStatusCode.NotFound);
    }

    [PostgreSqlFact]
    public async Task Project_names_are_unique_per_owner_on_create_and_update()
    {
        using var factory = database.CreateFactory();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        await RegisterAndLogin(owner);
        await RegisterAndLogin(other);
        var first = await CreateProject(owner, "Unique name");
        var second = await CreateProject(owner, "Other name");

        await AssertProblem(await owner.PostAsJsonAsync("/api/projects", new { name = "Unique name" }), HttpStatusCode.Conflict);
        await AssertProblem(await owner.PatchAsJsonAsync($"/api/projects/{second}", new { name = "Unique name" }), HttpStatusCode.Conflict);
        await ReadJson(await owner.PatchAsJsonAsync($"/api/projects/{first}", new { name = "Unique name" }), HttpStatusCode.OK);
        await CreateProject(other, "Unique name");
    }

    [PostgreSqlFact]
    public async Task Tasks_support_all_five_operations_due_dates_and_clearing_nullable_fields()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Tasks project");
        var taskRoot = $"/api/projects/{projectId}/tasks";
        var due = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(2));
        var taskId = await CreateTask(client, projectId, "Initial task", "Task description", due);
        var path = $"{taskRoot}/{taskId}";

        var task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
        Assert.Equal(projectId, task.GetProperty("projectId").GetGuid());
        Assert.Equal(0, task.GetProperty("status").GetInt32());
        Assert.Equal(due.ToUniversalTime(), task.GetProperty("dueAt").GetDateTimeOffset());
        var list = await ReadJson(await client.GetAsync(taskRoot), HttpStatusCode.OK);
        Assert.Single(list.EnumerateArray());

        using var statusUpdate = await client.PatchAsJsonAsync(path, new { status = 2 });
        Assert.Equal(HttpStatusCode.OK, statusUpdate.StatusCode);
        task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
        Assert.Equal("Task description", task.GetProperty("description").GetString());
        Assert.Equal(due.ToUniversalTime(), task.GetProperty("dueAt").GetDateTimeOffset());
        Assert.Equal(2, task.GetProperty("status").GetInt32());

        var changedDue = due.AddDays(1);
        using var dueUpdate = await client.PatchAsJsonAsync(path, new { dueAt = changedDue });
        Assert.Equal(HttpStatusCode.OK, dueUpdate.StatusCode);
        task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
        Assert.Equal(changedDue.ToUniversalTime(), task.GetProperty("dueAt").GetDateTimeOffset());

        using var cleared = await client.PatchAsJsonAsync(path,
            new { title = "Updated task", description = (string?)null, dueAt = (DateTimeOffset?)null });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
        Assert.Equal("Updated task", task.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, task.GetProperty("dueAt").ValueKind);

        using var deleted = await client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await AssertProblem(await client.GetAsync(path), HttpStatusCode.NotFound);
        await AssertProblem(await client.PatchAsJsonAsync(path, new { title = "Missing" }), HttpStatusCode.NotFound);
        await AssertProblem(await client.DeleteAsync(path), HttpStatusCode.NotFound);
        Assert.Empty((await ReadJson(await client.GetAsync(taskRoot), HttpStatusCode.OK)).EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task Invalid_project_and_task_requests_return_400_without_changing_data()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Unchanged");
        var taskId = await CreateTask(client, projectId, "Unchanged task");
        var projectPath = $"/api/projects/{projectId}";
        var taskRoot = $"{projectPath}/tasks";
        var taskPath = $"{taskRoot}/{taskId}";

        foreach (var value in new[] { "", "   ", new string('x', 151) })
        {
            await AssertValidation(await client.PostAsJsonAsync("/api/projects", new { name = value }));
            await AssertValidation(await client.PatchAsJsonAsync(projectPath, new { name = value }));
            await AssertValidation(await client.PostAsJsonAsync(taskRoot, new { title = value }));
            await AssertValidation(await client.PatchAsJsonAsync(taskPath, new { title = value }));
        }

        await AssertValidation(await client.PostAsJsonAsync("/api/projects", new { }));
        await AssertValidation(await client.PostAsJsonAsync(taskRoot, new { }));
        await AssertValidation(await client.PatchAsJsonAsync(projectPath, new { projectStatus = 999 }));
        await AssertValidation(await client.PatchAsJsonAsync(taskPath, new { status = -1 }));
        await AssertValidation(await client.PatchAsJsonAsync(taskPath, new { dueAt = "invalid-date" }));

        var project = (await ReadJson(await client.GetAsync(projectPath), HttpStatusCode.OK)).GetProperty("project");
        Assert.Equal("Unchanged", project.GetProperty("name").GetString());
        Assert.Equal(0, project.GetProperty("status").GetInt32());
        var tasks = await ReadJson(await client.GetAsync(taskRoot), HttpStatusCode.OK);
        Assert.Equal("Unchanged task", Assert.Single(tasks.EnumerateArray()).GetProperty("title").GetString());
        Assert.Single((await ReadJson(await client.GetAsync("/api/projects"), HttpStatusCode.OK)).GetProperty("projects").EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task Owners_and_project_ids_isolate_projects_and_tasks_for_every_operation()
    {
        using var factory = database.CreateFactory();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        await RegisterAndLogin(owner);
        await RegisterAndLogin(other);
        var projectId = await CreateProject(owner, "Private project");
        var otherProject = await CreateProject(owner, "Another project");
        var taskId = await CreateTask(owner, projectId, "Private task");
        var projectPath = $"/api/projects/{projectId}";
        var taskRoot = $"{projectPath}/tasks";
        var taskPath = $"{taskRoot}/{taskId}";

        Assert.Empty((await ReadJson(await other.GetAsync("/api/projects"), HttpStatusCode.OK)).GetProperty("projects").EnumerateArray());
        await AssertProblem(await other.GetAsync(projectPath), HttpStatusCode.NotFound);
        await AssertProblem(await other.PatchAsJsonAsync(projectPath, new { name = "Stolen" }), HttpStatusCode.NotFound);
        await AssertProblem(await other.DeleteAsync(projectPath), HttpStatusCode.NotFound);
        await AssertProblem(await other.GetAsync(taskRoot), HttpStatusCode.NotFound);
        await AssertProblem(await other.PostAsJsonAsync(taskRoot, new { title = "Stolen" }), HttpStatusCode.NotFound);
        await AssertProblem(await other.GetAsync(taskPath), HttpStatusCode.NotFound);
        await AssertProblem(await other.PatchAsJsonAsync(taskPath, new { title = "Stolen" }), HttpStatusCode.NotFound);
        await AssertProblem(await other.DeleteAsync(taskPath), HttpStatusCode.NotFound);

        var mismatched = $"/api/projects/{otherProject}/tasks/{taskId}";
        await AssertProblem(await owner.GetAsync(mismatched), HttpStatusCode.NotFound);
        await AssertProblem(await owner.PatchAsJsonAsync(mismatched, new { title = "Wrong project" }), HttpStatusCode.NotFound);
        await AssertProblem(await owner.DeleteAsync(mismatched), HttpStatusCode.NotFound);
        var original = await ReadJson(await owner.GetAsync(taskPath), HttpStatusCode.OK);
        Assert.Equal("Private task", original.GetProperty("title").GetString());
    }

    [PostgreSqlFact]
    public async Task Missing_projects_return_404_for_all_nested_task_operations()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var root = $"/api/projects/{Guid.NewGuid()}/tasks";
        var path = $"{root}/{Guid.NewGuid()}";

        await AssertProblem(await client.GetAsync(root), HttpStatusCode.NotFound);
        await AssertProblem(await client.PostAsJsonAsync(root, new { title = "Missing project" }), HttpStatusCode.NotFound);
        await AssertProblem(await client.GetAsync(path), HttpStatusCode.NotFound);
        await AssertProblem(await client.PatchAsJsonAsync(path, new { title = "Missing project" }), HttpStatusCode.NotFound);
        await AssertProblem(await client.DeleteAsync(path), HttpStatusCode.NotFound);
    }

    [PostgreSqlFact]
    public async Task Migrations_and_foreign_keys_enforce_relations_and_project_deletion_cascades()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Cascade project");
        var taskId = await CreateTask(client, projectId, "Cascade task");
        await using var context = database.CreateContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, (await context.Database.GetAppliedMigrationsAsync()).Count());
        var ownerId = await context.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        Assert.Equal(ownerId, (await context.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId)).OwnerId);
        Assert.Equal(projectId, (await context.TaskItems.AsNoTracking().SingleAsync(t => t.Id == taskId)).ProjectId);

        await using (var invalid = database.CreateContext())
        {
            invalid.TaskItems.Add(new TaskItem { Title = "Orphan", ProjectId = Guid.NewGuid() });
            await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
        }
        await using (var invalid = database.CreateContext())
        {
            invalid.Projects.Add(new Project { Name = "Orphan", OwnerId = Guid.NewGuid() });
            await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
        }
        await using (var restricted = database.CreateContext())
        {
            restricted.Users.Remove(await restricted.Users.SingleAsync(u => u.Id == ownerId));
            await Assert.ThrowsAsync<DbUpdateException>(() => restricted.SaveChangesAsync());
        }

        using var deleted = await client.DeleteAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await context.TaskItems.AnyAsync(t => t.Id == taskId));
    }

    [PostgreSqlFact]
    public async Task Unique_database_constraint_is_translated_even_when_precheck_is_bypassed()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = await RegisterAndLogin(client);
        await CreateProject(client, "Constraint test");
        await using var context = database.CreateContext();
        var ownerId = await context.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var repository = new ProjectRepository(context);

        await Assert.ThrowsAsync<ProjectAlreadyExistsException>(() => repository.AddAsync(
            new Project { OwnerId = ownerId, Name = "Constraint test" }, CancellationToken.None));
    }

    [PostgreSqlFact]
    public async Task Saved_user_project_and_task_survive_backend_host_restart()
    {
        string email;
        Guid projectId;
        Guid taskId;
        using (var firstHost = database.CreateFactory())
        {
            using var client = firstHost.CreateClient();
            email = await RegisterAndLogin(client);
            projectId = await CreateProject(client, "Persistent project");
            taskId = await CreateTask(client, projectId, "Persistent task");
        }

        using var restartedHost = database.CreateFactory();
        using var restartedClient = restartedHost.CreateClient();
        using var login = await restartedClient.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var project = (await ReadJson(await restartedClient.GetAsync($"/api/projects/{projectId}"), HttpStatusCode.OK)).GetProperty("project");
        Assert.Equal("Persistent project", project.GetProperty("name").GetString());
        var task = await ReadJson(await restartedClient.GetAsync($"/api/projects/{projectId}/tasks/{taskId}"), HttpStatusCode.OK);
        Assert.Equal("Persistent task", task.GetProperty("title").GetString());
        Assert.Equal(projectId, task.GetProperty("projectId").GetGuid());
    }

    [PostgreSqlFact]
    public async Task Auth_registration_login_and_me_have_working_documented_responses()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await AssertValidation(await client.PostAsJsonAsync("/api/auth/register",
            new { email = "invalid", nickname = "Test", password = Password }));
        using var rejectedLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "missing@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedLogin.StatusCode);

        var email = await RegisterAndLogin(client);
        var me = await ReadJson(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK);
        Assert.Equal(email, me.GetProperty("email").GetString());
        using var duplicate = await client.PostAsJsonAsync("/api/auth/register",
            new { email, nickname = "Test", password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var weakPassword = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@example.com", nickname = "Test", password = "weak" });
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
    }

}
