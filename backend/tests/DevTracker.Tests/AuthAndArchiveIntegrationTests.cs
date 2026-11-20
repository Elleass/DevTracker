using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class AuthAndArchiveIntegrationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Session_is_valid_before_expiration_and_rejected_after_30_minutes()
    {
        var clock = new ManualTimeProvider();
        using var factory = database.CreateFactory(clock);
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);

        clock.Advance(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(59));
        using var valid = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        clock.Advance(TimeSpan.FromSeconds(2));
        using var expired = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var projects = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, projects.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Active_requests_do_not_extend_the_absolute_session_lifetime()
    {
        var clock = new ManualTimeProvider();
        using var factory = database.CreateFactory(clock);
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);

        clock.Advance(TimeSpan.FromMinutes(16));
        using var active = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.False(active.Headers.Contains("Set-Cookie"));

        clock.Advance(TimeSpan.FromMinutes(15));
        using var expired = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Logout_deletes_cookie_and_protected_endpoints_require_sign_in_again()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = await RegisterAndLogin(client);
        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.Contains("DevTracker.Session=;") && value.Contains("expires="));

        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var projects = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, projects.StatusCode);
        using var repeatedLogout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, repeatedLogout.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ReadJson(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task Configured_session_lifetime_is_honored()
    {
        var clock = new ManualTimeProvider();
        using var factory = database.CreateFactory(clock).WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Authentication:Session:LifetimeMinutes"] = "5" })));
        using var client = factory.CreateClient();
        await RegisterAndLogin(client);

        clock.Advance(TimeSpan.FromMinutes(4));
        using var valid = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        using var expired = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Registration_always_assigns_USER_even_if_request_contains_ADMIN()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var registered = await ReadJson(await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, nickname = "Role test", password = Password, role = "ADMIN", roles = new[] { "ADMIN" }
        }), HttpStatusCode.Created);
        Assert.Equal("USER", Assert.Single(registered.GetProperty("roles").EnumerateArray()).GetString());

        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await ReadJson(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK);
        Assert.Equal("USER", Assert.Single(me.GetProperty("roles").EnumerateArray()).GetString());
        using var denied = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Only_ADMIN_can_list_users_and_provisioning_is_idempotent()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        using var anonymous = factory.CreateClient();
        using var noSession = await anonymous.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
        var email = await RegisterAndLogin(client);
        using var beforeGrant = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, beforeGrant.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Assert.True(await roles.RoleExistsAsync(ApplicationRoles.User));
            Assert.True(await roles.RoleExistsAsync(ApplicationRoles.Admin));
            var provisioner = scope.ServiceProvider.GetRequiredService<AdminRoleProvisioner>();
            await provisioner.GrantAsync(email);
            await provisioner.GrantAsync(email);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.GrantAsync("missing@example.com"));
        }

        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await ReadJson(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK);
        Assert.Contains(me.GetProperty("roles").EnumerateArray(), role => role.GetString() == "ADMIN");
        var users = await ReadJson(await client.GetAsync("/api/admin/users"), HttpStatusCode.OK);
        Assert.Contains(users.EnumerateArray(), user => user.GetProperty("email").GetString() == email);
        Assert.All(users.EnumerateArray(), user => Assert.False(user.TryGetProperty("passwordHash", out _)));
    }

    [PostgreSqlFact]
    public async Task Validation_covers_description_lengths_and_auth_required_fields()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        await AssertValidation(await client.PostAsJsonAsync("/api/auth/register",
            new { email = "", nickname = "", password = "" }));
        await AssertValidation(await client.PostAsJsonAsync("/api/auth/register",
            new { email = "valid@example.com", nickname = new string('x', 101), password = Password }));
        await AssertValidation(await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "" }));
        await RegisterAndLogin(client);
        var projectId = await CreateProject(client, "Validation descriptions");
        var taskId = await CreateTask(client, projectId, "Validation task");
        var tooLong = new string('x', 4001);
        await AssertValidation(await client.PostAsJsonAsync("/api/projects", new { name = "Invalid", description = tooLong }));
        await AssertValidation(await client.PatchAsJsonAsync($"/api/projects/{projectId}", new { description = tooLong }));
        await AssertValidation(await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { title = "Invalid", description = tooLong }));
        await AssertValidation(await client.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{taskId}", new { description = tooLong }));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
