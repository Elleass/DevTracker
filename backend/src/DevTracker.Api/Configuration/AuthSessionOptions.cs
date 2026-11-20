using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.Configuration;

public sealed class AuthSessionOptions
{
    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; } = 30;
}
