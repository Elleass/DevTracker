using Microsoft.AspNetCore.Identity;

namespace DevTracker.Infrastructure.Identity;

public sealed class AdminRoleProvisioner(UserManager<ApplicationUser> users)
{
    public async Task GrantAsync(string email)
    {
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Register the user before granting the ADMIN role.");

        if (await users.IsInRoleAsync(user, ApplicationRoles.Admin)) return;

        var result = await users.AddToRoleAsync(user, ApplicationRoles.Admin);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to grant the ADMIN role.");
        }
    }
}
