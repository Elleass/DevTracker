using System.ComponentModel.DataAnnotations;

namespace DevTracker.Api.DTOs.Auth
{
    public class LoginRequest
    {
        public required string Email {get; set;}
        public required string Password {get; set;}
    }
}