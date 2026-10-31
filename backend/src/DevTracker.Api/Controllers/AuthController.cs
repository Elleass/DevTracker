using DevTracker.Api.DTOs.Auth;
using DevTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevTracker.Api.Controllers
{        
    
    
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
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

            return StatusCode(StatusCodes.Status201Created, new
            {
                appUser.Id,
                appUser.Email,
                appUser.Nickname
            });

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if(user == null)
            {
                return Unauthorized();
            }

           var result = await _signInManager.PasswordSignInAsync(user, request.Password, false, false);
           if (result.Succeeded == false)
            {
                return Unauthorized();
            }
            return Ok();
        }


        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                user.Id,
                user.Email,
                user.Nickname
            });
        }
    }
}