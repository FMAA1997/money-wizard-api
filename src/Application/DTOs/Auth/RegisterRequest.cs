using Application.DTOs.Profile;

namespace Application.DTOs.Auth;

public sealed record RegisterRequest(
    string Name,
    DateOnly DateOfBirth,
    string Country,
    ArgentinaProfilePayload? Argentina);
