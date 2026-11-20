using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.DTOs
{
    public class CreateTaskItemRequest
    {
        [Required]
        [StringLength(150)]
        public required string Title {get; set;}
        [StringLength(4000)]
        public string? Description {get; set;}
        public DateTimeOffset? DueAt { get; set; }
        [Range(1, 525600)]
        public int? EstimatedMinutes { get; set; }
        [EnumDataType(typeof(DevTracker.Domain.Entities.TaskPriority))]
        public DevTracker.Domain.Entities.TaskPriority Priority { get; set; } = DevTracker.Domain.Entities.TaskPriority.Normal;

    }
}
