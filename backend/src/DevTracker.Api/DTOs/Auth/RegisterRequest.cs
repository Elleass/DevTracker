namespace DevTracker.Api.DTOs.Auth
{
    public class RegisterRequest
    {
        public required string Email {get; init;}
        public required string Nickname {get; init; }
        public required string Password {get; init;}


    }
}