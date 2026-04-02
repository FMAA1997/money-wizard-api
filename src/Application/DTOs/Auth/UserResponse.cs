namespace Application.DTOs.Auth;

public sealed record UserResponse(Guid Id, string Name, string Email, DateOnly DateOfBirth);
