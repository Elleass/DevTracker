using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.DTOs
{
    public class CreateProjectRequest
    {
        public required string Name {get; set;}
        public string? Description {get; set;}




    }
}