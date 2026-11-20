using DevTracker.Domain.Entities;

namespace DevTracker.Application.Models;

public sealed record WorkSessionResponse(Guid Id, Guid ProjectId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long DurationSeconds)
{
    public static WorkSessionResponse From(WorkSession session) => new(session.Id, session.ProjectId, session.StartedAt, session.EndedAt, session.DurationSeconds);
}
public sealed record DocumentResponse(Guid Id, Guid ProjectId, string Title, string ContentMarkdown, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static DocumentResponse From(Document document) => new(document.Id, document.ProjectId, document.Title, document.ContentMarkdown, document.CreatedAt, document.UpdatedAt);
}
public sealed record WorkSessionCompleted(Guid SessionId, Guid ProjectId, Guid UserId);
public sealed record TaskDependencyResponse(Guid TaskId, Guid PredecessorTaskId);
