namespace DevTracker.Infrastructure.Identity;

public static class ApplicationRoles
{
    public const string User = "USER";
    public const string Admin = "ADMIN";

    public static readonly Guid UserRoleId = new("d11f9eb6-0e68-40b5-bb49-d40fda7c7311");
    public static readonly Guid AdminRoleId = new("c4b56538-9dc8-4e95-a9f0-e853ca5bd574");
}
