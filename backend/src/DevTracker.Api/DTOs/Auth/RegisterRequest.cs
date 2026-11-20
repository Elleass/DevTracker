using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public required string Email {get; init;}
        [Required, StringLength(100)]
        public required string Nickname {get; init; }
        [Required]
        public required string Password {get; init;}


    }
}
