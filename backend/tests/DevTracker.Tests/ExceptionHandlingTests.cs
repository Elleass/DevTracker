using System.Net;
using System.Text.Json;
using DevTracker.Api.Exceptions;
using DevTracker.Application.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevTracker.Tests;

public class ExceptionHandlingTests
{
    [Theory]
    [InlineData("project", 404, "Project not found")]
    [InlineData("task", 404, "Task not found")]
    [InlineData("duplicate", 409, "Project already exists")]
    [InlineData("unfinished", 409, "Cannot archive a project with unfinished tasks")]
    [InlineData("archived", 409, "Cannot create or update tasks in an archived project")]
    [InlineData("unexpected", 500, "An unexpected error occurred")]
    [InlineData("update", 500, "An unexpected error occurred")]
    [InlineData("bad-request", 400, "Invalid request")]
    [InlineData("too-large", 413, "Invalid request")]
    public async Task Middleware_maps_exceptions_to_safe_problem_details(
        string error, int expectedStatus, string expectedTitle)
    {
        await using var app = await CreateApp(error);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/failing-request");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var problem = json.RootElement;

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal("/failing-request", problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("internal-secret", body);
        Assert.DoesNotContain("stackTrace", body);
    }

    [Fact]
    public async Task Unsupported_accept_header_still_returns_problem_details()
    {
        await using var app = await CreateApp("project");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using var response = await client.GetAsync("/failing-request");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Project not found", json.RootElement.GetProperty("title").GetString());
    }

    private static async Task<WebApplication> CreateApp(string error)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.Run(_ => throw CreateException(error));
        await app.StartAsync();
        return app;
    }

    private static Exception CreateException(string error) => error switch
    {
        "project" => new ProjectDoNotExistsException(),
        "task" => new TaskDoNotExistsException(),
        "duplicate" => new ProjectAlreadyExistsException(),
        "unfinished" => new ProjectHasUnfinishedTasksException(),
        "archived" => new ProjectArchivedException(),
        "update" => new FailedToUpdateProjectException(),
        "bad-request" => new BadHttpRequestException("internal-secret"),
        "too-large" => new BadHttpRequestException("internal-secret", 413),
        _ => new InvalidOperationException("internal-secret")
    };
}
