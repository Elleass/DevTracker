namespace DevTracker.Domain.Entities;

public sealed class TaskDependency
{
    public Guid TaskId { get; set; }
    public Guid PredecessorTaskId { get; set; }
}
