using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.DTOs
{
    public class CreateProjectRequest
    {
        [Required]
        [StringLength(150)]
        public required string Name {get; set;}
        [StringLength(4000)]
        public string? Description {get; set;}




    }
}
