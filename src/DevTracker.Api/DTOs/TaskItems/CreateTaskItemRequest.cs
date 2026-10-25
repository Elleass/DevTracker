using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.DTOs
{
    public class CreateTaskItemRequest
    {
        public required string Title {get; set;}
        public string? Description {get; set;}

    }
}