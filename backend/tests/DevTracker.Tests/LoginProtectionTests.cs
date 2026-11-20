using System.Net;
using System.Net.Http.Json;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static DevTracker.Tests.ApiTestClient;

namespace DevTracker.Tests;

[Trait("Category", "PostgreSQL")]
public class LoginProtectionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Five_failed_passwords_lock_account_and_expired_lockout_allows_login()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = await Register(client);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-password-42!" });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        using var correctButLocked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, correctButLocked.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByEmailAsync(email))!;
            Assert.True(await manager.IsLockedOutAsync(user));
            Assert.InRange(user.LockoutEnd!.Value, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
            // Expire the persisted lockout without a real 15-minute wait.
            Assert.True((await manager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        }
        using var allowed = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Successful_login_resets_failed_attempt_counter()
    {
        using var factory = database.CreateFactory();
        using var client = factory.CreateClient();
        var email = await Register(client);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-password-42!" });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        await using (var before = factory.Services.CreateAsyncScope())
        {
            var manager = before.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Equal(2, await manager.GetAccessFailedCountAsync((await manager.FindByEmailAsync(email))!));
        }
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var after = factory.Services.CreateAsyncScope();
        var users = after.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(0, await users.GetAccessFailedCountAsync((await users.FindByEmailAsync(email))!));
    }

    [PostgreSqlFact]
    public async Task Configured_lockout_threshold_and_duration_are_applied()
    {
        using var factory = WithProtection(new()
        {
            ["MaxFailedAttempts"] = "2", ["LockoutMinutes"] = "1"
        });
        using var client = factory.CreateClient();
        var email = await Register(client);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-password-42!" });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        using var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.InRange(user.LockoutEnd!.Value, DateTimeOffset.UtcNow.AddSeconds(45), DateTimeOffset.UtcNow.AddSeconds(75));
    }

    [PostgreSqlFact]
    public async Task Rate_limit_returns_429_and_does_not_execute_the_blocked_login()
    {
        using var factory = WithProtection(new() { ["PermitLimit"] = "2" });
        using var client = factory.CreateClient();
        var email = await Register(client);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{attempt + 1}");
            using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-password-42!" });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.99");
        using var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.NotNull(blocked.Headers.RetryAfter?.Delta);
        await AssertProblem(blocked, HttpStatusCode.TooManyRequests);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(2, await users.GetAccessFailedCountAsync((await users.FindByEmailAsync(email))!));
        // The policy only applies to login, not to registration or the rest of the API.
        await Register(client);
    }

    [PostgreSqlFact]
    public async Task Rate_limit_is_partitioned_by_connection_IP()
    {
        using var factory = WithProtection(new() { ["PermitLimit"] = "1" }).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestPeerAddress>()));
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        first.DefaultRequestHeaders.Add("X-Test-Peer", "192.0.2.1");
        second.DefaultRequestHeaders.Add("X-Test-Peer", "192.0.2.2");
        using var allowedFirst = await first.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, allowedFirst.StatusCode);
        await AssertProblem(await first.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password }), HttpStatusCode.TooManyRequests);
        using var allowedSecond = await second.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, allowedSecond.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Rate_limit_window_replenishes()
    {
        using var factory = WithProtection(new() { ["PermitLimit"] = "1", ["WindowSeconds"] = "1" });
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        await AssertProblem(await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password }), HttpStatusCode.TooManyRequests);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        using var replenished = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, replenished.StatusCode);
    }

    private WebApplicationFactory<Program> WithProtection(Dictionary<string, string?> values) =>
        database.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(values.ToDictionary(pair => $"Authentication:LoginProtection:{pair.Key}", pair => pair.Value))));

    private static async Task<string> Register(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await ReadJson(await client.PostAsJsonAsync("/api/auth/register",
            new { email, nickname = "Protection test", password = Password }), HttpStatusCode.Created);
        return email;
    }

    private sealed class TestPeerAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Peer"].ToString());
                await continuation(context);
            });
            next(app);
        };
    }
}
