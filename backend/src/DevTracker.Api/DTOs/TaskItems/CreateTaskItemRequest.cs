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

    }
}
