namespace Application.DTOs.Auth;

public sealed record RegisterRequest(string Name, DateOnly DateOfBirth);
