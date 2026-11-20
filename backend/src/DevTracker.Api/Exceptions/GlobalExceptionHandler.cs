using DevTracker.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.Exceptions;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ProjectDoNotExistsException =>
                (StatusCodes.Status404NotFound, "Project not found"),
            TaskDoNotExistsException =>
                (StatusCodes.Status404NotFound, "Task not found"),
            ProjectAlreadyExistsException =>
                (StatusCodes.Status409Conflict, "Project already exists"),
            ProjectHasUnfinishedTasksException =>
                (StatusCodes.Status409Conflict, "Cannot archive a project with unfinished tasks"),
            ProjectArchivedException =>
                (StatusCodes.Status409Conflict, "Cannot create or update tasks in an archived project"),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "Invalid request"),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = httpContext.Request.Path.Value,
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        };

        httpContext.Response.StatusCode = status;

        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        }))
        {
            // Keep a JSON error response even when Accept doesn't match a registered writer.
            await httpContext.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }

        return true;
    }
}
