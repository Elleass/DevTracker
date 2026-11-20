using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevTracker.Tests;

internal static class ApiTestClient
{
    internal const string Password = "Test-only-password-42!";

    internal static async Task<string> RegisterAndLogin(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await ReadJson(await client.PostAsJsonAsync("/api/auth/register",
            new { email, nickname = "Integration test", password = Password }), HttpStatusCode.Created);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.Contains("Set-Cookie"));
        return email;
    }

    internal static async Task<Guid> CreateProject(HttpClient client, string name, string? description = null)
    {
        using var response = await client.PostAsJsonAsync("/api/projects", new { name, description });
        Assert.NotNull(response.Headers.Location);
        var body = await ReadJson(response, HttpStatusCode.Created);
        return body.GetProperty("projectId").GetGuid();
    }

    internal static async Task<Guid> CreateTask(HttpClient client, Guid projectId, string title,
        string? description = null, DateTimeOffset? dueAt = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { title, description, dueAt });
        Assert.NotNull(response.Headers.Location);
        var body = await ReadJson(response, HttpStatusCode.Created);
        return body.GetProperty("taskId").GetGuid();
    }

    internal static async Task<JsonElement> ReadJson(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {body}");
            using var json = JsonDocument.Parse(body);
            return json.RootElement.Clone();
        }
    }

    internal static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJson(response, status);
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    internal static async Task AssertValidation(HttpResponseMessage response)
    {
        var body = await ReadJson(response, HttpStatusCode.BadRequest);
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateObject());
    }
}
