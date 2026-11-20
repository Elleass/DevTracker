using DevTracker.Api.DTOs;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Application.Models;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "Authentication cookie is missing, invalid, or the user no longer exists.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json", Description = "Unexpected server exception. Details are logged with traceId.")]
    public class TasksItemController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITaskItemService _taskService;

        public TasksItemController(UserManager<ApplicationUser> userManager, ITaskItemService taskService)
        {
            _userManager = userManager;
            _taskService = taskService;
        }

        [HttpPost]
        [EndpointDescription("Creates a ToDo task. Optional estimatedMinutes: 1–525600; priority: 0=Low, 1=Normal (default), 2=High. DueAt is retained for compatibility; the UI uses estimates instead of deadlines.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json", Description = "ProjectArchivedException: cannot create tasks in an archived project.")]
        [ProducesResponseType(typeof(CreatedTaskItemResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "Invalid request body or empty task title.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException: the project does not exist or belongs to another user.")]
        public async Task<IActionResult> AddTaskItemAsync(Guid projectId, [FromBody] CreateTaskItemRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            Guid taskId = await _taskService.AddTaskItemAsync(projectId, user.Id,
                request.Title, request.Description, request.DueAt, cancellationToken, request.EstimatedMinutes, request.Priority);
            return CreatedAtAction(nameof(GetTaskItem), new { projectId, taskItemId = taskId },
                new CreatedTaskItemResponse(taskId));
        }

        [HttpGet]
        [EndpointDescription("Lists tasks owned by the current user within the specified project. Defaults: pageNumber=1, pageSize=20; maximum pageSize=100. Optional status (0=ToDo, 1=InProgress, 2=Done) and case-insensitive literal title substring search. Filters are applied before pagination. Ordered by CreatedAt then Id. A page beyond the last returns empty items with the actual filtered totalCount.")]
        [ProducesResponseType(typeof(PagedResult<GetTaskItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "Invalid pagination, status, or search query. pageNumber must be positive; pageSize must be 1–100; search must have at most 150 characters.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException: the project does not exist or belongs to another user.")]
        public async Task<IActionResult> GetTasksItemAsync(Guid projectId, [FromQuery] TaskListRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var page = await _taskService.GetTaskItemsAsync(projectId, user.Id,
                new TaskListQuery(request.PageNumber, request.PageSize, request.Status, request.Search), cancellationToken);

            return Ok(new PagedResult<GetTaskItemResponse>(
                page.Items.Select(GetTaskItemResponse.FromEntity).ToList(),
                page.TotalCount, page.PageNumber, page.PageSize));
        }

        [HttpGet("{taskItemId:guid}")]
        [ProducesResponseType(typeof(GetTaskItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException or TaskDoNotExistsException: the project is inaccessible or the task does not exist in this project.")]
        public async Task<IActionResult> GetTaskItem(Guid projectId, Guid taskItemId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Unauthorized();
            }

            var task = await _taskService.GetTaskItemAsync(projectId, taskItemId, user.Id, cancellationToken);
            return Ok(GetTaskItemResponse.FromEntity(task));
        }

        [HttpDelete("{taskItemId:guid}")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json", Description = "TaskDependencyException: another task requires this predecessor; remove the dependency first.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException or TaskDoNotExistsException: the project is inaccessible or the task does not exist in this project.")]
        public async Task<IActionResult> DeleteTaskItemAsync(Guid taskItemId, Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            await _taskService.DeleteTaskItemAsync(projectId, taskItemId, user.Id, cancellationToken);

            return NoContent();
        }

        [HttpPatch("{taskItemId:guid}")]
        [EndpointDescription("Updates specified fields only. estimatedMinutes: 1–525600, null clears the estimate; priority: 0=Low, 1=Normal, 2=High. Omitted fields retain their current values.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json", Description = "ProjectArchivedException or TaskDependencyException: archived project, unfinished predecessors, or reopening a predecessor of a completed task.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request body is invalid.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException or TaskDoNotExistsException: the project is inaccessible or the task does not exist in this project.")]
        public async Task<IActionResult> UpdateTaskItemAsync(Guid projectId, Guid taskItemId, UpdateTaskItemRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            await _taskService.UpdateTaskItemAsync(projectId, taskItemId, user.Id,
                request.Title, request.Description, request.Status, request.DescriptionSpecified,
                request.DueAt, request.DueAtSpecified, cancellationToken, request.EstimatedMinutes, request.EstimatedMinutesSpecified, request.Priority);
            return Ok();

        }
    }
}
