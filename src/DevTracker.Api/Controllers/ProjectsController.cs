using DevTracker.Api.DTOs;
using DevTracker.Application.Exceptions;
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
        public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            try
            {
                Guid projectId = await _projectService.CreateProjectAsync(
                    user.Id, 
                    request.Name,
                    request.Description, 
                    cancellationToken);

                return StatusCode(StatusCodes.Status201Created, new {projectId});

            }
            catch (ProjectAlreadyExistsException ex)
            {
                return Conflict( new {message = ex.Message});
            }            
        }


        [HttpGet]
        public async Task<IActionResult> GetAllProjects(CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            List<Project> projects = await _projectService.GetProjectsAsync(user.Id, cancellationToken);
            return StatusCode(StatusCodes.Status200OK, new
            {
                projects
            });
        } 


        [HttpGet("{projectId:guid}")]
        public async Task<IActionResult> GetProjectById(Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            try
            {
                Project project = await  _projectService.GetProjectAsync(projectId, user.Id, cancellationToken);
                return StatusCode(StatusCodes.Status200OK, new
                {
                    project
                });
            }
            catch(ProjectDoNotExistsException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("{projectId:guid}")]
        public async Task<IActionResult> DeleteProject(Guid projectId, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            try
            {
                await  _projectService.DeleteProjectAsync(projectId, user.Id, cancellationToken);
                return NoContent();
            }
            catch(ProjectDoNotExistsException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPatch("{projectId:guid}")]
        public async Task<IActionResult> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            try
            {
                 await _projectService.UpdateProjectAsync(
                    projectId,
                    user.Id, 
                    request.Name,
                    request.Description, 
                    cancellationToken);

                return Ok(new {projectId, request.Name, request.Description});

            }
            catch (ProjectDoNotExistsException ex)
            {
                return NotFound(new { message = ex.Message });

            }  
        }


        
        
    }
}