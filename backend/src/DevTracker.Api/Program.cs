using DevTracker.Api.Exceptions;
using DevTracker.Api.Configuration;
using DevTracker.Application.Inerfaces;
using DevTracker.Application.Services;
using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using DevTracker.Infrastructure.Persistence;
using DevTracker.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DevTracker.Infrastructure.Background;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["CookieAuth"] = new OpenApiSecurityScheme
        { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "DevTracker.Session" };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("CookieAuth", context.Document)] = [] }];
        return Task.CompletedTask;
    });
});
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("DevTracker");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddOptions<LoginProtectionOptions>()
    .BindConfiguration("Authentication:LoginProtection")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<IdentityOptions>()
    .Configure<IOptions<LoginProtectionOptions>>((options, protection) =>
    {
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = protection.Value.MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(protection.Value.LockoutMinutes);
    });
builder.Services.AddRateLimiter(options =>
    options.AddPolicy<string, LoginRateLimitPolicy>(LoginProtectionOptions.PolicyName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AdminRoleProvisioner>();
builder.Services.AddOptions<AuthSessionOptions>()
    .BindConfiguration("Authentication:Session")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<IOptions<AuthSessionOptions>>((options, session) =>
    {
        options.Cookie.Name = "DevTracker.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(session.Value.LifetimeMinutes);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ITaskItemRepository, TaskItemRepository>();
builder.Services.AddScoped<ITaskItemService, TaskItemService>();
builder.Services.AddScoped<IProjectWriteLock, PostgreSqlProjectWriteLock>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddSingleton<WorkSessionQueue>();
builder.Services.AddSingleton<IWorkSessionEvents>(services => services.GetRequiredService<WorkSessionQueue>());
if (builder.Configuration.GetValue("BackgroundProcessing:Enabled", true))
    builder.Services.AddHostedService<WorkStatisticsWorker>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}

if (builder.Configuration["grant-admin"] is { Length: > 0 } adminEmail)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AdminRoleProvisioner>().GrantAsync(adminEmail);
    app.Logger.LogInformation("ADMIN role granted. Sign in again to receive the updated role.");
    return;
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "DevTracker API v1"));
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.UseHttpsRedirection();

app.Run();

public partial class Program;
