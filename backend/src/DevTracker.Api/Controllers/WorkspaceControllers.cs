using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DevTracker.Api.Validation;
using DevTracker.Application.Models;
using DevTracker.Application.Services;
using DevTracker.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.Controllers;

[ApiController, Authorize]
[ProducesResponseType(typeof(ValidationProblemDetails), 400, "application/problem+json")]
[ProducesResponseType(401)]
[ProducesResponseType(typeof(ProblemDetails), 404, "application/problem+json", Description = "Project, document, session or task is missing or belongs to another user.")]
[ProducesResponseType(typeof(ProblemDetails), 409, "application/problem+json", Description = "ActiveWorkSessionException, ProjectArchivedException or TaskDependencyException: operation violates a business rule.")]
[ProducesResponseType(typeof(ProblemDetails), 500, "application/problem+json")]
public abstract class WorkspaceControllerBase : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed class PageRequest : IValidatableObject
{
    [FromQuery(Name = "pageNumber"), Range(1, int.MaxValue)] public int PageNumber { get; set; } = 1;
    [FromQuery(Name = "pageSize"), Range(1, 100)] public int PageSize { get; set; } = 20;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (((long)PageNumber - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("Page offset is too large", [nameof(PageNumber)]);
    }
}
public sealed class DocumentRequest
{
    [Required, StringLength(150), NotWhiteSpace] public string Title { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(100000)] public string ContentMarkdown { get; set; } = "";
}
public sealed class DependencyRequest : IValidatableObject
{
    public Guid PredecessorTaskId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (PredecessorTaskId == Guid.Empty)
            yield return new ValidationResult("A predecessor task ID is required", [nameof(PredecessorTaskId)]);
    }
}

[Route("api/work-sessions")]
public sealed class WorkSessionsController(WorkspaceService workspace) : WorkspaceControllerBase
{
    [HttpPost("/api/projects/{projectId:guid}/work-sessions")]
    [ProducesResponseType(typeof(WorkSessionResponse), 201)]
    public async Task<IActionResult> Start(Guid projectId, CancellationToken ct)
    {
        var result = await workspace.StartAsync(projectId, UserId, ct);
        return CreatedAtAction(nameof(Get), new { sessionId = result.Id }, result);
    }
    [HttpGet("active")]
    [ProducesResponseType(typeof(WorkSessionResponse), 200)]
    public async Task<IActionResult> Active(CancellationToken ct) =>
        new JsonResult(await workspace.ActiveAsync(UserId, ct));
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(WorkSessionResponse), 200)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken ct) => Ok(await workspace.SessionAsync(sessionId, UserId, ct));
    [HttpPost("{sessionId:guid}/stop")]
    [ProducesResponseType(typeof(WorkSessionResponse), 200)]
    public async Task<IActionResult> Stop(Guid sessionId, CancellationToken ct) => Ok(await workspace.StopAsync(sessionId, UserId, ct));
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WorkSessionResponse>), 200)]
    public async Task<IActionResult> History([FromQuery] PageRequest request, [FromQuery] Guid? projectId, CancellationToken ct) =>
        Ok(await workspace.HistoryAsync(UserId, projectId, request.PageNumber, request.PageSize, ct));
}

[Route("api/projects/{projectId:guid}/documents")]
public sealed class DocumentsController(WorkspaceService workspace) : WorkspaceControllerBase
{
    [HttpGet, ProducesResponseType(typeof(List<DocumentResponse>), 200)]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct) => Ok(await workspace.DocumentsAsync(projectId, UserId, ct));
    [HttpGet("{documentId:guid}"), ProducesResponseType(typeof(DocumentResponse), 200)]
    public async Task<IActionResult> Get(Guid projectId, Guid documentId, CancellationToken ct) => Ok(await workspace.DocumentAsync(projectId, documentId, UserId, ct));
    [HttpPost, ProducesResponseType(typeof(DocumentResponse), 201)]
    public async Task<IActionResult> Create(Guid projectId, DocumentRequest request, CancellationToken ct)
    {
        var result = await workspace.SaveDocumentAsync(projectId, null, UserId, request.Title, request.ContentMarkdown, ct);
        return CreatedAtAction(nameof(Get), new { projectId, documentId = result.Id }, result);
    }
    [HttpPut("{documentId:guid}"), ProducesResponseType(typeof(DocumentResponse), 200)]
    public async Task<IActionResult> Update(Guid projectId, Guid documentId, DocumentRequest request, CancellationToken ct) =>
        Ok(await workspace.SaveDocumentAsync(projectId, documentId, UserId, request.Title, request.ContentMarkdown, ct));
    [HttpDelete("{documentId:guid}"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid projectId, Guid documentId, CancellationToken ct)
    {
        await workspace.DeleteDocumentAsync(projectId, documentId, UserId, ct); return NoContent();
    }
}

[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/dependencies")]
public sealed class DependenciesController(WorkspaceService workspace) : WorkspaceControllerBase
{
    [HttpGet, ProducesResponseType(typeof(List<TaskDependencyResponse>), 200)]
    public async Task<IActionResult> List(Guid projectId, Guid taskId, CancellationToken ct) => Ok(await workspace.DependenciesAsync(projectId, taskId, UserId, ct));
    [HttpPost, ProducesResponseType(204)]
    public async Task<IActionResult> Add(Guid projectId, Guid taskId, DependencyRequest request, CancellationToken ct)
    {
        await workspace.ChangeDependencyAsync(projectId, taskId, request.PredecessorTaskId, UserId, false, ct); return NoContent();
    }
    [HttpDelete("{predecessorId:guid}"), ProducesResponseType(204)]
    public async Task<IActionResult> Remove(Guid projectId, Guid taskId, Guid predecessorId, CancellationToken ct)
    {
        await workspace.ChangeDependencyAsync(projectId, taskId, predecessorId, UserId, true, ct); return NoContent();
    }
}
