using System.Net;
using System.Net.Http.Json;
using DevTracker.Application.Inerfaces;
using DevTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class ProjectConcurrencyTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public Task Archive_first_rejects_concurrent_task_creation() => RunRace(archiveFirst: true, updateExisting: false);

    [PostgreSqlFact]
    public Task Archive_first_rejects_concurrent_task_reopening() => RunRace(archiveFirst: true, updateExisting: true);

    [PostgreSqlFact]
    public Task Task_creation_first_prevents_concurrent_archive() => RunRace(archiveFirst: false, updateExisting: false);

    [PostgreSqlFact]
    public Task Task_reopening_first_prevents_concurrent_archive() => RunRace(archiveFirst: false, updateExisting: true);

    private async Task RunRace(bool archiveFirst, bool updateExisting)
    {
        var gate = new WriteGate();
        using var factory = database.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IProjectWriteLock>(provider =>
                new GatedWriteLock(new PostgreSqlProjectWriteLock(provider.GetRequiredService<AppDbContext>()), gate))));
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Concurrent archive");
        Guid? taskId = null;
        if (updateExisting)
        {
            taskId = await CreateTask(client, projectId, "Initially done");
            using var done = await client.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{taskId}", new { status = 2 });
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        }

        gate.Enabled = true;
        Task<HttpResponseMessage> Archive() => client.PostAsync($"/api/projects/{projectId}/archive", null);
        Task<HttpResponseMessage> ChangeTask() => updateExisting
            ? client.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{taskId}", new { status = 0 })
            : client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { title = "Concurrent ToDo" });

        var first = archiveFirst ? Archive() : ChangeTask();
        Task<HttpResponseMessage> second;
        try
        {
            await gate.FirstAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = archiveFirst ? ChangeTask() : Archive();
            // Observe a real PostgreSQL lock wait, rather than relying on request timing or sleeps.
            await WaitForBlockedWriter();
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        using var firstResponse = await first.WaitAsync(TimeSpan.FromSeconds(10));
        using var secondResponse = await second.WaitAsync(TimeSpan.FromSeconds(10));
        if (archiveFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
            await AssertProblem(secondResponse, HttpStatusCode.Conflict);
        }
        else
        {
            Assert.Equal(updateExisting ? HttpStatusCode.OK : HttpStatusCode.Created, firstResponse.StatusCode);
            await AssertProblem(secondResponse, HttpStatusCode.Conflict);
        }

        await using var context = database.CreateContext();
        var project = await context.Projects.SingleAsync(project => project.Id == projectId);
        var unfinished = await context.TaskItems.AnyAsync(task => task.ProjectId == projectId && task.Status != Domain.Entities.TaskItemStatus.Done);
        Assert.Equal(archiveFirst, project.ArchivedAt is not null);
        Assert.Equal(!archiveFirst, unfinished);

        // Rejected requests must release/roll back their write scope, leaving the project usable.
        using var rename = await client.PatchAsJsonAsync($"/api/projects/{projectId}", new { name = "Lock released" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    private async Task WaitForBlockedWriter()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var context = database.CreateContext();
        while (true)
        {
            var blocked = await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::integer AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                    AND query LIKE '%FOR UPDATE%'
                """).SingleAsync(timeout.Token);
            if (blocked > 0) return;
            await Task.Delay(25, timeout.Token);
        }
    }

    private sealed class WriteGate
    {
        public bool Enabled { get; set; }
        public int Attempts;
        public TaskCompletionSource FirstAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedWriteLock(IProjectWriteLock inner, WriteGate gate) : IProjectWriteLock
    {
        public async Task<IProjectWriteScope> AcquireAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken)
        {
            var shouldPause = gate.Enabled && Interlocked.Increment(ref gate.Attempts) == 1;
            var scope = await inner.AcquireAsync(projectId, ownerId, cancellationToken);
            if (!shouldPause) return scope;
            try
            {
                gate.FirstAcquired.TrySetResult();
                await gate.Release.Task.WaitAsync(cancellationToken);
                return scope;
            }
            catch
            {
                await scope.DisposeAsync();
                throw;
            }
        }
    }
}
