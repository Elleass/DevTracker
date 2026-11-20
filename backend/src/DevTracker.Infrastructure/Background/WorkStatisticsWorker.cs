using DevTracker.Application.Inerfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DevTracker.Infrastructure.Background;

public sealed class WorkStatisticsWorker(WorkSessionQueue queue, IServiceScopeFactory scopes, ILogger<WorkStatisticsWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BackgroundProcessing:Enabled", true)) return;
        var reconcileAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= reconcileAt)
                {
                    await Rebuild(null, stoppingToken);
                    reconcileAt = DateTimeOffset.UtcNow.AddSeconds(30);
                }
                while (queue.Reader.TryRead(out var completed)) await Rebuild(completed.ProjectId, stoppingToken);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                wait.CancelAfter(TimeSpan.FromSeconds(30));
                try { await queue.Reader.WaitToReadAsync(wait.Token); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Work statistics failed; persisted sessions will be reconciled on retry");
                reconcileAt = DateTimeOffset.MinValue;
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
    private async Task Rebuild(Guid? projectId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().RebuildStatisticsAsync(projectId, ct);
    }
}
