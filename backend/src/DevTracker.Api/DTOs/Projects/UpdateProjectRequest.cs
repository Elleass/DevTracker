using DevTracker.Domain.Entities;

namespace DevTracker.Api.DTOs
{
    public class UpdateProjectRequest
    {
        public required string? Name {get; set;}
        public string? Description {get; set;}
        public ProjectStatus ProjectStatus {get;set; } 
    }
}