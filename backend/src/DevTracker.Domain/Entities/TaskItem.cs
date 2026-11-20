namespace DevTracker.Domain.Entities
{
    public class TaskItem
    {
        public Guid Id {get; set;}
        public Guid ProjectId {get;set;}
        public required string Title {get;set;}
        public string? Description {get; set;}
        public TaskItemStatus Status {get; set;} = TaskItemStatus.ToDo;
        public DateTimeOffset CreatedAt{get;set;}
        public DateTimeOffset UpdatedAt {get; set;}
        public DateTimeOffset? DueAt {get; set;}
        public int? EstimatedMinutes { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    }

    public enum TaskItemStatus
    {
        ToDo,
        InProgress,
        Done
    }

    public enum TaskPriority { Low, Normal, High }


}
