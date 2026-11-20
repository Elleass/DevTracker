using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class TaskPlanningTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Estimates_and_all_priorities_persist_and_patch_preserves_omitted_fields()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var project = await CreateProject(client, "Task planning");
        var root = $"/api/projects/{project}/tasks";
        for (var priority = 0; priority <= 2; priority++)
        {
            var created = await ReadJson(await client.PostAsJsonAsync(root,
                new { title = $"Priority {priority}", priority, estimatedMinutes = 90 }), HttpStatusCode.Created);
            var path = $"{root}/{created.GetProperty("taskId").GetGuid()}";
            var task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
            Assert.Equal(priority, task.GetProperty("priority").GetInt32());
            Assert.Equal(90, task.GetProperty("estimatedMinutes").GetInt32());
            using var patch = await client.PatchAsJsonAsync(path, new { title = "Renamed" });
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
            task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
            Assert.Equal(priority, task.GetProperty("priority").GetInt32());
            Assert.Equal(90, task.GetProperty("estimatedMinutes").GetInt32());
            using var cleared = await client.PatchAsJsonAsync(path, new { estimatedMinutes = (int?)null, priority = 2 });
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
            task = await ReadJson(await client.GetAsync(path), HttpStatusCode.OK);
            Assert.Equal(JsonValueKind.Null, task.GetProperty("estimatedMinutes").ValueKind);
            Assert.Equal(2, task.GetProperty("priority").GetInt32());
            await AssertValidation(await client.PatchAsJsonAsync(path, new { estimatedMinutes = -1 }));
            await AssertValidation(await client.PatchAsJsonAsync(path, new { priority = 3 }));
        }
        await AssertValidation(await client.PostAsJsonAsync(root, new { title = "Invalid", estimatedMinutes = 0 }));
        await AssertValidation(await client.PostAsJsonAsync(root, new { title = "Invalid", estimatedMinutes = 525601 }));
        await AssertValidation(await client.PostAsJsonAsync(root, new { title = "Invalid", priority = -1 }));
        var legacy = await CreateTask(client, project, "No metadata");
        var defaultTask = await ReadJson(await client.GetAsync($"{root}/{legacy}"), HttpStatusCode.OK);
        Assert.Equal(1, defaultTask.GetProperty("priority").GetInt32());
        Assert.Equal(JsonValueKind.Null, defaultTask.GetProperty("estimatedMinutes").ValueKind);
    }
}
