using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.DTOs.Auth
{
    public class LoginRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public required string Email {get; set;}
        [Required]
        public required string Password {get; set;}
    }
}
