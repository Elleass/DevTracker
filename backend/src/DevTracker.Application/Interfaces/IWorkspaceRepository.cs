using DevTracker.Domain.Entities;
using DevTracker.Application.Models;

namespace DevTracker.Application.Inerfaces;

public interface IWorkspaceRepository
{
    Task<WorkSession?> ActiveSessionAsync(Guid userId, CancellationToken ct);
    Task<WorkSession?> SessionAsync(Guid id, Guid userId, CancellationToken ct);
    Task SaveSessionAsync(WorkSession session, bool isNew, CancellationToken ct);
    Task<PagedResult<WorkSession>> SessionsAsync(Guid userId, Guid? projectId, int page, int size, CancellationToken ct);
    Task<List<Document>> DocumentsAsync(Guid projectId, CancellationToken ct);
    Task<Document?> DocumentAsync(Guid id, Guid projectId, CancellationToken ct);
    Task SaveDocumentAsync(Document document, bool isNew, CancellationToken ct);
    Task DeleteDocumentAsync(Document document, CancellationToken ct);
    Task<List<TaskDependency>> DependenciesAsync(Guid projectId, CancellationToken ct);
    Task AddDependencyAsync(TaskDependency dependency, CancellationToken ct);
    Task DeleteDependencyAsync(TaskDependency dependency, CancellationToken ct);
    Task RebuildStatisticsAsync(Guid? projectId, CancellationToken ct);
}

public interface IWorkSessionEvents
{
    bool Publish(WorkSessionCompleted completed);
}
