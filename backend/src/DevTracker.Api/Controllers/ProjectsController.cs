using DevTracker.Api.DTOs;
using DevTracker.Application.Services;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/projects")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "Authentication cookie is missing, invalid, or the user no longer exists.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json", Description = "Unexpected server exception. Details are logged with traceId.")]
    public class ProjectsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ProjectService _projectService;

        public ProjectsController(UserManager<ApplicationUser> userManager, ProjectService projectService)
        {
            _userManager = userManager;
            _projectService = projectService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreatedProjectResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request body is invalid.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json", Description = "ProjectAlreadyExistsException: this user already has a project with the given name.")]
        public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            Guid projectId = await _projectService.CreateProjectAsync(
                user.Id,
                request.Name,
                request.Description,
                cancellationToken);

            return CreatedAtAction(nameof(GetProjectById), new { projectId }, new CreatedProjectResponse(projectId));

        }

        [HttpGet]
        [ProducesResponseType(typeof(ProjectListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllProjects(CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            List<Project> projects = await _projectService.GetProjectsAsync(user.Id, cancellationToken);
            return Ok(new ProjectListResponse(projects));
        }

        [HttpGet("{projectId:guid}")]
        [ProducesResponseType(typeof(ProjectDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException: the project does not exist or belongs to another user.")]
        public async Task<IActionResult> GetProjectById(Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            Project project = await  _projectService.GetProjectAsync(projectId, user.Id, cancellationToken);
            return Ok(new ProjectDetailsResponse(project));
        }

        [HttpDelete("{projectId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException: the project does not exist or belongs to another user.")]
        public async Task<IActionResult> DeleteProject(Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            await  _projectService.DeleteProjectAsync(projectId, user.Id, cancellationToken);
            return NoContent();
        }

        [HttpPatch("{projectId:guid}")]
        [ProducesResponseType(typeof(UpdatedProjectResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json", Description = "ProjectAlreadyExistsException: this user already has another project with the given name.")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request body is invalid.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json", Description = "ProjectDoNotExistsException: the project does not exist or belongs to another user.")]
        public async Task<IActionResult> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            var project = await _projectService.UpdateProjectAsync(
                projectId,
                user.Id,
                request.Name,
                request.Description,
                request.DescriptionSpecified,
                request.ProjectStatus,
                cancellationToken);

            return Ok(new UpdatedProjectResponse(project.Id, project.Name, project.Description, project.Status));

        }

    }
}
