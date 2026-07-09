namespace ArcadeLink.Contracts;

public record UserCreatedEvent(Guid UserId, string Name, string Email);
