using DevTracker.Domain.Entities;

namespace DevTracker.Api.DTOs
{
    public class GetTaskItemResponse
    {
        public string? Title {get; set;}
        public string? Description {get; set;}
        public DateTimeOffset CreatedAt {get; set;}
        public DateTimeOffset UpdatedAt {get; set;}
        public Guid Id {get; set;}
        public Guid ProjectId {get; set;}        
        public TaskItemStatus Status {get; set;}
        public DateTimeOffset? DueAt {get; set;}

        public static GetTaskItemResponse FromEntity(TaskItem task) => new()
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            DueAt = task.DueAt
        };
    }
}
