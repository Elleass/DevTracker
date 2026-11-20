using System.Net;
using System.Net.Http.Json;
using DevTracker.Application.Exceptions;
using DevTracker.Application.Services;
using DevTracker.Application.Models;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Background;
using DevTracker.Application.Inerfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using DevTracker.Infrastructure.Identity;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

public class DependencyRuleTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    [Fact] public void Self_dependency_is_rejected() => Assert.Throws<TaskDependencyException>(() => DependencyRules.ValidateEdge(A, A, []));
    [Fact] public void Cycle_is_rejected() => Assert.Throws<TaskDependencyException>(() => DependencyRules.ValidateEdge(C, A,
        [new() { TaskId = A, PredecessorTaskId = B }, new() { TaskId = B, PredecessorTaskId = C }]));
    [Fact] public void Independent_edge_is_allowed() => DependencyRules.ValidateEdge(A, B, [new() { TaskId = C, PredecessorTaskId = B }]);
    [Theory, InlineData(TaskItemStatus.ToDo), InlineData(TaskItemStatus.InProgress)]
    public void Incomplete_predecessor_blocks_done(TaskItemStatus status) => Assert.Throws<TaskDependencyException>(() =>
        DependencyRules.ValidateStatus(A, TaskItemStatus.Done,
            [new() { Id = A, Title = "A" }, new() { Id = B, Title = "B", Status = status }], [new() { TaskId = A, PredecessorTaskId = B }]));
    [Fact] public void Done_predecessor_allows_completion() => DependencyRules.ValidateStatus(A, TaskItemStatus.Done,
        [new() { Id = A, Title = "A" }, new() { Id = B, Title = "B", Status = TaskItemStatus.Done }], [new() { TaskId = A, PredecessorTaskId = B }]);
    [Fact] public void Completed_dependent_prevents_reopening() => Assert.Throws<TaskDependencyException>(() =>
        DependencyRules.ValidateStatus(B, TaskItemStatus.ToDo,
            [new() { Id = A, Title = "A", Status = TaskItemStatus.Done }, new() { Id = B, Title = "B", Status = TaskItemStatus.Done }],
            [new() { TaskId = A, PredecessorTaskId = B }]));
    [Fact] public void Queue_is_bounded_and_accepts_again_after_drain()
    {
        var queue = new WorkSessionQueue(); var item = new WorkSessionCompleted(A, B, C);
        for (var i = 0; i < 256; i++) Assert.True(queue.Publish(item));
        Assert.False(queue.Publish(item)); Assert.True(queue.Reader.TryRead(out _)); Assert.True(queue.Publish(item));
    }
    [Fact] public void Duration_comes_from_server_timestamps()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Equal(65, new WorkSession { StartedAt = start, EndedAt = start.AddSeconds(65.9) }.DurationSeconds);
        Assert.Equal(0, new WorkSession { StartedAt = start }.DurationSeconds);
    }
}

[Trait("Category", "PostgreSQL")]
public class WorkspaceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Sessions_are_unique_across_projects_even_for_parallel_starts_and_stop_is_idempotent()
    {
        using var factory = database.CreateFactory(); using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var a = await CreateProject(client, "Work A"); var b = await CreateProject(client, "Work B");
        var responses = await Task.WhenAll(client.PostAsync($"/api/projects/{a}/work-sessions", null), client.PostAsync($"/api/projects/{b}/work-sessions", null));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await AssertProblem(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        var session = await ReadJson(responses.Single(r => r.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created);
        var id = session.GetProperty("id").GetGuid();
        var active = await ReadJson(await client.GetAsync("/api/work-sessions/active"), HttpStatusCode.OK);
        Assert.Equal(id, active.GetProperty("id").GetGuid());
        var stopped = await ReadJson(await client.PostAsync($"/api/work-sessions/{id}/stop", null), HttpStatusCode.OK);
        var repeated = await ReadJson(await client.PostAsync($"/api/work-sessions/{id}/stop", null), HttpStatusCode.OK);
        Assert.Equal(stopped.GetProperty("endedAt").GetString(), repeated.GetProperty("endedAt").GetString());
        var history = await ReadJson(await client.GetAsync("/api/work-sessions?pageSize=1"), HttpStatusCode.OK);
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        await AssertValidation(await client.GetAsync("/api/work-sessions?pageSize=101"));
        using var next = await client.PostAsync($"/api/projects/{b}/work-sessions", null);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Session_ownership_and_archived_projects_are_protected()
    {
        using var factory = database.CreateFactory(); using var owner = factory.CreateClient(); using var other = factory.CreateClient();
        await RegisterAndLogin(owner); await RegisterAndLogin(other);
        var project = await CreateProject(owner, "Private work");
        var session = await ReadJson(await owner.PostAsync($"/api/projects/{project}/work-sessions", null), HttpStatusCode.Created);
        var id = session.GetProperty("id").GetGuid();
        await AssertProblem(await other.GetAsync($"/api/work-sessions/{id}"), HttpStatusCode.NotFound);
        await AssertProblem(await other.PostAsync($"/api/work-sessions/{id}/stop", null), HttpStatusCode.NotFound);
        await AssertProblem(await other.GetAsync($"/api/work-sessions?projectId={project}"), HttpStatusCode.NotFound);
        using var archived = await owner.PostAsync($"/api/projects/{project}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        await ReadJson(await owner.PostAsync($"/api/work-sessions/{id}/stop", null), HttpStatusCode.OK);
        await AssertProblem(await owner.PostAsync($"/api/projects/{project}/work-sessions", null), HttpStatusCode.Conflict);
    }

    [PostgreSqlFact]
    public async Task Document_CRUD_validation_and_ownership_work()
    {
        using var factory = database.CreateFactory(); using var owner = factory.CreateClient(); using var other = factory.CreateClient();
        await RegisterAndLogin(owner); await RegisterAndLogin(other);
        var project = await CreateProject(owner, "Docs"); var root = $"/api/projects/{project}/documents";
        var document = await ReadJson(await owner.PostAsJsonAsync(root, new { title = "Notes", contentMarkdown = "# Hello" }), HttpStatusCode.Created);
        var path = $"{root}/{document.GetProperty("id").GetGuid()}";
        var read = await ReadJson(await owner.GetAsync(path), HttpStatusCode.OK);
        Assert.Equal("# Hello", read.GetProperty("contentMarkdown").GetString());
        await ReadJson(await owner.PutAsJsonAsync(path, new { title = "Updated", contentMarkdown = "**Saved**" }), HttpStatusCode.OK);
        var list = await ReadJson(await owner.GetAsync(root), HttpStatusCode.OK);
        Assert.Single(list.EnumerateArray());
        await AssertValidation(await owner.PostAsJsonAsync(root, new { title = " ", contentMarkdown = "" }));
        await AssertValidation(await owner.PutAsJsonAsync(path, new { title = "Valid", contentMarkdown = new string('x', 100001) }));
        await AssertProblem(await other.GetAsync(path), HttpStatusCode.NotFound);
        await AssertProblem(await other.DeleteAsync(path), HttpStatusCode.NotFound);
        using var deleted = await owner.DeleteAsync(path); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await AssertProblem(await owner.GetAsync(path), HttpStatusCode.NotFound);
    }

    [PostgreSqlFact]
    public async Task Dependency_graph_enforces_completion_cycles_and_safe_deletion()
    {
        using var factory = database.CreateFactory(); using var client = factory.CreateClient(); await RegisterAndLogin(client);
        var project = await CreateProject(client, "Graph"); var otherProject = await CreateProject(client, "Other graph");
        var a = await CreateTask(client, project, "A"); var b = await CreateTask(client, project, "B");
        var foreign = await CreateTask(client, otherProject, "Foreign");
        var root = $"/api/projects/{project}/tasks";
        await AssertValidation(await client.PostAsJsonAsync($"{root}/{a}/dependencies", new { }));
        using var added = await client.PostAsJsonAsync($"{root}/{a}/dependencies", new { predecessorTaskId = b });
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        await AssertProblem(await client.PostAsJsonAsync($"{root}/{b}/dependencies", new { predecessorTaskId = a }), HttpStatusCode.Conflict);
        await AssertProblem(await client.PostAsJsonAsync($"{root}/{a}/dependencies", new { predecessorTaskId = a }), HttpStatusCode.Conflict);
        await AssertProblem(await client.PostAsJsonAsync($"{root}/{a}/dependencies", new { predecessorTaskId = foreign }), HttpStatusCode.NotFound);
        await AssertProblem(await client.PatchAsJsonAsync($"{root}/{a}", new { status = 2 }), HttpStatusCode.Conflict);
        await AssertProblem(await client.DeleteAsync($"{root}/{b}"), HttpStatusCode.Conflict);
        using var bDone = await client.PatchAsJsonAsync($"{root}/{b}", new { status = 2 }); Assert.Equal(HttpStatusCode.OK, bDone.StatusCode);
        using var aDone = await client.PatchAsJsonAsync($"{root}/{a}", new { status = 2 }); Assert.Equal(HttpStatusCode.OK, aDone.StatusCode);
        await AssertProblem(await client.PatchAsJsonAsync($"{root}/{b}", new { status = 0 }), HttpStatusCode.Conflict);
        using var removed = await client.DeleteAsync($"{root}/{a}/dependencies/{b}"); Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var bDelete = await client.DeleteAsync($"{root}/{b}"); Assert.Equal(HttpStatusCode.NoContent, bDelete.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Statistics_worker_processes_duplicates_and_recovers_projection_after_restart()
    {
        Guid project; Guid sessionId;
        using (var factory = database.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "true" }))))
        {
            using var client = factory.CreateClient(); await RegisterAndLogin(client); project = await CreateProject(client, "Stats");
            var session = await ReadJson(await client.PostAsync($"/api/projects/{project}/work-sessions", null), HttpStatusCode.Created);
            sessionId = session.GetProperty("id").GetGuid();
            await using (var db = database.CreateContext())
            {
                var entity = await db.WorkSessions.SingleAsync(x => x.Id == sessionId); entity.StartedAt = DateTimeOffset.UtcNow.AddSeconds(-65);
                await db.SaveChangesAsync();
            }
            await ReadJson(await client.PostAsync($"/api/work-sessions/{sessionId}/stop", null), HttpStatusCode.OK);
            var queue = factory.Services.GetRequiredService<IWorkSessionEvents>();
            queue.Publish(new(sessionId, project, Guid.Empty)); queue.Publish(new(sessionId, project, Guid.Empty));
            await WaitForStatistics(project);
        }
        await using (var db = database.CreateContext())
            await db.Projects.Where(x => x.Id == project).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.TotalWorkedSeconds, 0));
        using var restarted = database.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "true" })));
        using var trigger = restarted.CreateClient();
        await WaitForStatistics(project);
    }

    private async Task WaitForStatistics(Guid project)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var db = database.CreateContext();
            var seconds = await db.Projects.Where(x => x.Id == project).Select(x => x.TotalWorkedSeconds).SingleAsync(timeout.Token);
            if (seconds >= 65) { Assert.InRange(seconds, 65, 70); return; }
            await Task.Delay(40, timeout.Token);
        }
    }

    [PostgreSqlFact]
    public async Task Worker_logs_storage_failure_and_recovers_on_retry()
    {
        Guid projectId;
        await using (var db = database.CreateContext())
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"worker-{Guid.NewGuid():N}", Nickname = "Worker test" };
            db.Users.Add(user);
            var project = new Project { OwnerId = user.Id, Name = "Worker recovery" }; db.Projects.Add(project);
            db.WorkSessions.Add(new WorkSession { UserId = user.Id, ProjectId = project.Id, StartedAt = DateTimeOffset.UtcNow.AddSeconds(-65), EndedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); projectId = project.Id;
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkSessions\" RENAME TO \"OfflineWorkSessions\"");
        }
        var errors = new WorkerErrors();
        using var factory = database.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "true" }));
            builder.ConfigureLogging(logging => logging.AddProvider(errors));
        });
        try
        {
            using var client = factory.CreateClient();
            using var swagger = await client.GetAsync("/swagger/index.html");
            Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
            await errors.Observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await using var db = database.CreateContext();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"OfflineWorkSessions\" RENAME TO \"WorkSessions\"");
        }
        await WaitForStatistics(projectId);
    }

    private sealed class WorkerErrors : ILoggerProvider
    {
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }
        private sealed class Capture(WorkerErrors owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (level == LogLevel.Error && category.Contains(nameof(WorkStatisticsWorker))) owner.Observed.TrySetResult();
            }
        }
    }
}
