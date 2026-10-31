using DevTracker.Application.Services;
using DevTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.DTOs
{
    public class UpdateTaskItemRequest
    {
        public string? Title {get; set;}
        public string? Description {get; set;}
        public TaskItemStatus? Status  {get; set;}
        public DateTimeOffset? DueAt {get;set;}


    }
}