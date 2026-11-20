using DevTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace DevTracker.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVTRACKER_TEST_CONNECTION")))
        {
            Skip = "Set DEVTRACKER_TEST_CONNECTION to a PostgreSQL admin connection to run database tests.";
        }
    }
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"devtracker_tests_{Guid.NewGuid():N}";
    private readonly string? _adminConnection = Environment.GetEnvironmentVariable("DEVTRACKER_TEST_CONNECTION");
    private bool _created;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;

        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _created = true;

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection)
        {
            Database = _databaseName
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public WebApplicationFactory<Program> CreateFactory(TimeProvider? timeProvider = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "false" }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                if (timeProvider is not null)
                {
                    services.AddSingleton(timeProvider);
                    services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(
                        options => options.TimeProvider = timeProvider);
                    services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                        Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
                        options => options.TimeProvider = timeProvider);
                }
            });
        });

    public async Task DisposeAsync()
    {
        if (!_created) return;

        // Only drop the isolated, generated database created by this fixture.
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
