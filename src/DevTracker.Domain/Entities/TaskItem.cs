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
    
    }

    public enum TaskItemStatus
    {
        ToDo,
        InProgress,
        Done
    }


}