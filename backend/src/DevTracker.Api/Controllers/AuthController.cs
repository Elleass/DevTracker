using DevTracker.Api.DTOs.Auth;
using DevTracker.Infrastructure.Identity;
using DevTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DevTracker.Api.Configuration;

namespace DevTracker.Api.Controllers
{

    [ApiController]
    [Route("api/auth")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json", Description = "Unexpected server exception. Details are logged with traceId.")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _dbContext;
        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
            AppDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Description = "Invalid request body (ValidationProblemDetails) or Identity validation errors (an array of IdentityError).")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            var appUser = new ApplicationUser
            {
                Email = request.Email,
                Nickname = request.Nickname,
                UserName = request.Email

            };
            var result = await _userManager.CreateAsync(appUser, request.Password);
            if (result.Succeeded == false)
            {
                return BadRequest(result.Errors);
            }

            var roleResult = await _userManager.AddToRoleAsync(appUser, ApplicationRoles.User);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to assign the default USER role.");
            }
            await transaction.CommitAsync(HttpContext.RequestAborted);

            return StatusCode(StatusCodes.Status201Created,
                new UserResponse(appUser.Id, appUser.Email, appUser.Nickname, [ApplicationRoles.User]));

        }

        [HttpPost("login")]
        [EnableRateLimiting(LoginProtectionOptions.PolicyName)]
        [ProducesResponseType(StatusCodes.Status200OK, Description = "Signed in. The response sets the Identity authentication cookie used by protected endpoints.")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request body is invalid.")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "Invalid credentials or a temporarily locked account. Five failed passwords lock the account for 15 minutes by default.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json", Description = "IP login request limit exceeded (default: 10 per 60 seconds). Retry-After specifies when to retry.")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if(user == null)
            {
                return Unauthorized();
            }

           var result = await _signInManager.PasswordSignInAsync(user, request.Password,
               isPersistent: false, lockoutOnFailure: true);
           if (result.Succeeded == false)
            {
                return Unauthorized();
            }
            return Ok();
        }

        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "Authentication cookie is missing, invalid, or the user no longer exists.")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserResponse(user.Id, user.Email, user.Nickname, roles.ToArray()));
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Signed out. Deletes the current browser authentication cookie.")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Description = "The session is missing or expired.")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return NoContent();
        }
    }
}
