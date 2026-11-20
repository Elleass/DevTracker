namespace DevTracker.Api.DTOs.Auth;

public sealed record UserResponse(Guid Id, string? Email, string Nickname, IReadOnlyCollection<string> Roles);
