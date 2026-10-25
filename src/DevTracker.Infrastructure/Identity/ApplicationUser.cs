using Microsoft.AspNetCore.Identity;

namespace DevTracker.Infrastructure.Identity
{
public class ApplicationUser : IdentityUser<Guid>
{
    public string Nickname { get; set; } = string.Empty;
}
}