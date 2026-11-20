using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DevTracker.Tests;

public class ApiDocumentationTests
{
    [Fact]
    public async Task Development_serves_swagger_assets_and_documented_error_responses()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/swagger/index.html");
        Assert.Contains("Swagger UI", html);
        using var initializer = await client.GetAsync("/swagger/index.js");
        initializer.EnsureSuccessStatusCode();
        var script = await initializer.Content.ReadAsStringAsync();
        Assert.Contains("/openapi/v1.json", script);
        using var bundle = await client.GetAsync("/swagger/swagger-ui-bundle.js");
        bundle.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal("cookie", document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("CookieAuth").GetProperty("in").GetString());
        Assert.True(paths.GetProperty("/api/projects").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
        var listTasks = paths.GetProperty("/api/projects/{projectId}/tasks").GetProperty("get");
        var parameterNames = listTasks.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()).ToArray();
        Assert.Contains("pageNumber", parameterNames);
        Assert.Contains("pageSize", parameterNames);
        Assert.Contains("status", parameterNames);
        Assert.Contains("search", parameterNames);
        Assert.True(listTasks.GetProperty("responses").TryGetProperty("400", out _));
        var create = paths.GetProperty("/api/projects").GetProperty("post").GetProperty("responses");
        Assert.Contains("ProjectAlreadyExistsException",
            create.GetProperty("409").GetProperty("description").GetString());
        Assert.True(create.TryGetProperty("201", out _));
        Assert.True(create.TryGetProperty("400", out _));
        Assert.True(create.TryGetProperty("401", out _));
        Assert.True(create.TryGetProperty("500", out _));

        var update = paths.GetProperty("/api/projects/{projectId}/tasks/{taskItemId}")
            .GetProperty("patch").GetProperty("responses").GetProperty("404");
        Assert.Contains("ProjectDoNotExistsException", update.GetProperty("description").GetString());
        Assert.Contains("TaskDoNotExistsException", update.GetProperty("description").GetString());
        Assert.True(update.GetProperty("content").TryGetProperty("application/problem+json", out _));

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.True(operation.Value.GetProperty("responses").TryGetProperty("500", out _));
            }
        }

        using var unauthorized = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Production_does_not_expose_swagger_or_openapi()
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var swagger = await client.GetAsync("/swagger/index.html");
        using var openApi = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "false" }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
}
