using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
using DevTracker.Application.Models;
using DevTracker.Domain.Entities;

namespace DevTracker.Application.Services;

public sealed class WorkspaceService(IProjectRepository projects, ITaskItemRepository tasks,
    IWorkspaceRepository repository, IProjectWriteLock writeLock, TimeProvider clock, IWorkSessionEvents events)
{
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow().ToUniversalTime();
        return now.AddTicks(-(now.Ticks % 10)); // PostgreSQL stores microseconds, not .NET 100ns ticks.
    }
    private async Task<Project> OwnProject(Guid projectId, Guid ownerId, CancellationToken ct) =>
        await projects.GetByIdAsync(projectId, ownerId, ct) ?? throw new ProjectDoNotExistsException();

    public async Task<WorkSessionResponse> StartAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await using var scope = await writeLock.AcquireAsync(projectId, userId, ct);
        var project = await OwnProject(projectId, userId, ct);
        if (project.ArchivedAt is not null) throw new ArchivedWorkSessionException();
        if (await repository.ActiveSessionAsync(userId, ct) is not null) throw new ActiveWorkSessionException();
        var session = new WorkSession { UserId = userId, ProjectId = projectId, StartedAt = Now() };
        await repository.SaveSessionAsync(session, true, ct);
        await scope.CommitAsync(ct);
        return WorkSessionResponse.From(session);
    }

    public async Task<WorkSessionResponse> StopAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var existing = await repository.SessionAsync(id, userId, ct) ?? throw new WorkSessionNotFoundException();
        await using var scope = await writeLock.AcquireAsync(existing.ProjectId, userId, ct);
        await OwnProject(existing.ProjectId, userId, ct);
        var session = await repository.SessionAsync(id, userId, ct) ?? throw new WorkSessionNotFoundException();
        if (session.EndedAt is null)
        {
            var now = Now();
            session.EndedAt = now < session.StartedAt ? session.StartedAt : now;
            await repository.SaveSessionAsync(session, false, ct);
        }
        await scope.CommitAsync(ct);
        // Queue saturation or a crash cannot lose source data; the worker also reconciles persisted sessions.
        events.Publish(new WorkSessionCompleted(session.Id, session.ProjectId, session.UserId));
        return WorkSessionResponse.From(session);
    }

    public async Task<WorkSessionResponse?> ActiveAsync(Guid userId, CancellationToken ct) =>
        await repository.ActiveSessionAsync(userId, ct) is { } session ? WorkSessionResponse.From(session) : null;

    public async Task<WorkSessionResponse> SessionAsync(Guid id, Guid userId, CancellationToken ct) =>
        WorkSessionResponse.From(await repository.SessionAsync(id, userId, ct) ?? throw new WorkSessionNotFoundException());

    public async Task<PagedResult<WorkSessionResponse>> HistoryAsync(Guid userId, Guid? projectId, int page, int size, CancellationToken ct)
    {
        if (projectId is { } id) await OwnProject(id, userId, ct);
        var result = await repository.SessionsAsync(userId, projectId, page, size, ct);
        return new(result.Items.Select(WorkSessionResponse.From).ToList(), result.TotalCount, page, size);
    }

    public async Task<List<DocumentResponse>> DocumentsAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await OwnProject(projectId, userId, ct);
        return (await repository.DocumentsAsync(projectId, ct)).Select(DocumentResponse.From).ToList();
    }

    public async Task<DocumentResponse> DocumentAsync(Guid projectId, Guid id, Guid userId, CancellationToken ct)
    {
        await OwnProject(projectId, userId, ct);
        return DocumentResponse.From(await repository.DocumentAsync(id, projectId, ct) ?? throw new DocumentNotFoundException());
    }

    public async Task<DocumentResponse> SaveDocumentAsync(Guid projectId, Guid? id, Guid userId, string title, string content, CancellationToken ct)
    {
        await using var scope = await writeLock.AcquireAsync(projectId, userId, ct);
        await OwnProject(projectId, userId, ct);
        var now = Now();
        var document = id is { } existingId
            ? await repository.DocumentAsync(existingId, projectId, ct) ?? throw new DocumentNotFoundException()
            : new Document { ProjectId = projectId, CreatedByUserId = userId, CreatedAt = now };
        document.Title = title;
        document.ContentMarkdown = content;
        document.UpdatedAt = now;
        await repository.SaveDocumentAsync(document, id is null, ct);
        await scope.CommitAsync(ct);
        return DocumentResponse.From(document);
    }

    public async Task DeleteDocumentAsync(Guid projectId, Guid id, Guid userId, CancellationToken ct)
    {
        await using var scope = await writeLock.AcquireAsync(projectId, userId, ct);
        await OwnProject(projectId, userId, ct);
        var document = await repository.DocumentAsync(id, projectId, ct) ?? throw new DocumentNotFoundException();
        await repository.DeleteDocumentAsync(document, ct);
        await scope.CommitAsync(ct);
    }

    public async Task<List<TaskDependencyResponse>> DependenciesAsync(Guid projectId, Guid taskId, Guid userId, CancellationToken ct)
    {
        await OwnProject(projectId, userId, ct);
        _ = await tasks.GetByIdAsync(taskId, projectId, ct) ?? throw new TaskDoNotExistsException();
        return (await repository.DependenciesAsync(projectId, ct)).Where(link => link.TaskId == taskId)
            .Select(link => new TaskDependencyResponse(link.TaskId, link.PredecessorTaskId)).ToList();
    }

    public async Task ChangeDependencyAsync(Guid projectId, Guid taskId, Guid predecessorId, Guid userId, bool remove, CancellationToken ct)
    {
        await using var scope = await writeLock.AcquireAsync(projectId, userId, ct);
        var project = await OwnProject(projectId, userId, ct);
        if (project.ArchivedAt is not null) throw new ProjectArchivedException();
        var task = await tasks.GetByIdAsync(taskId, projectId, ct) ?? throw new TaskDoNotExistsException();
        var predecessor = await tasks.GetByIdAsync(predecessorId, projectId, ct) ?? throw new TaskDoNotExistsException();
        var links = await repository.DependenciesAsync(projectId, ct);
        var existing = links.SingleOrDefault(link => link.TaskId == taskId && link.PredecessorTaskId == predecessorId);
        if (remove)
        {
            if (existing is not null) await repository.DeleteDependencyAsync(existing, ct);
        }
        else if (existing is null)
        {
            DependencyRules.ValidateEdge(taskId, predecessorId, links);
            if (task.Status == TaskItemStatus.Done && predecessor.Status != TaskItemStatus.Done)
                throw new TaskDependencyException("A completed task cannot depend on an unfinished predecessor");
            await repository.AddDependencyAsync(new TaskDependency { TaskId = taskId, PredecessorTaskId = predecessorId }, ct);
        }
        await scope.CommitAsync(ct);
    }
}
