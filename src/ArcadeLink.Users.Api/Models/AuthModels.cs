namespace ArcadeLink.Users.Api.Models;

public record RegisterRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiresAtUtc);

public record UserResponse(Guid Id, string Name, string Email, string Role, DateTime CreatedAtUtc);
