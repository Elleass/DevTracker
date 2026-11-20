using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.Configuration;

public sealed class LoginProtectionOptions
{
    public const string PolicyName = "login";

    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;

    [Range(1, 1000)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
