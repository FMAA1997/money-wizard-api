namespace Application.DTOs.Profile;

public sealed record UpdateProfileRequest(
    string Name,
    DateOnly DateOfBirth,
    string Country,
    ArgentinaProfilePayload? Argentina);
