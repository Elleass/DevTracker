namespace DevTracker.Domain.Entities;

public sealed class WorkSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public long DurationSeconds => EndedAt is { } end ? Math.Max(0, (long)(end - StartedAt).TotalSeconds) : 0;
}
