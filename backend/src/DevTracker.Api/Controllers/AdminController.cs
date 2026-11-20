using DevTracker.Api.DTOs.Auth;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/admin")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "The session is missing or expired.")]
[ProducesResponseType(StatusCodes.Status403Forbidden, Description = "The user does not have the ADMIN role.")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json", Description = "Unexpected server exception. Details are logged with traceId.")]
public sealed class AdminController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var records = await users.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(cancellationToken);
        var response = new List<UserResponse>(records.Count);
        foreach (var user in records)
        {
            var roles = await users.GetRolesAsync(user);
            response.Add(new UserResponse(user.Id, user.Email, user.Nickname, roles.ToArray()));
        }
        return Ok(response);
    }
}
