using DevTracker.Api.DTOs;
using DevTracker.Application.Exceptions;
using DevTracker.Application.Inerfaces;
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

            try
            {
                Guid taskId = await _taskService.AddTaskItemAsync(projectId, user.Id, request.Title, request.Description, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, new { taskId }); c
            }
            catch (ProjectDoNotExistsException)
            {
                return NotFound();
            }



        }
    }
}