using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DevTracker.Api.Configuration;

public sealed class LoginRateLimitPolicy(IOptions<LoginProtectionOptions> configuration) : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configuration.Value.PermitLimit,
                Window = TimeSpan.FromSeconds(configuration.Value.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many login attempts",
            instance: context.HttpContext.Request.Path.Value,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.HttpContext.TraceIdentifier
            }).ExecuteAsync(context.HttpContext);
    };
}
