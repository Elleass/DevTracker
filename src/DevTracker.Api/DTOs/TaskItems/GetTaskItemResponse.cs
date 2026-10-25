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
        public DateTimeOffset DueAt {get; set;}
    }
}