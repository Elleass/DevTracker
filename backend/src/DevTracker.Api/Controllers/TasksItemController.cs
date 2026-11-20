using DevTracker.Api.DTOs;
using DevTracker.Application.Inerfaces;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace DevTracker.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/projects/{projectId:guid}/tasks")]
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
        public async Task<IActionResult> AddTaskItemAsync(Guid projectId, [FromBody] CreateTaskItemRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Task title cannot be empty.");
            }

            Guid taskId = await _taskService.AddTaskItemAsync(projectId, user.Id, request.Title, request.Description, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { taskId });
        }

        [HttpGet]
        public async Task<IActionResult> GetTasksItemAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            List<TaskItem> taskItems = await _taskService.GetTaskItemsAsync(projectId, user.Id, cancellationToken);

            return Ok(taskItems);
        }

        [HttpDelete("{taskItemId:guid}")]
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
        public async Task<IActionResult> UpdateTaskItemAsync(Guid projectId, Guid taskItemId, UpdateTaskItemRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            await _taskService.UpdateTaskItemAsync(projectId, taskItemId, user.Id, request.Title, request.Description,request.Status, cancellationToken);
            return Ok();


        }
    }
}
