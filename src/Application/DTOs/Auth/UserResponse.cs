using Application.DTOs.Profile;

namespace Application.DTOs.Auth;

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    DateOnly DateOfBirth,
    string Country,
    IReadOnlyList<string> DisplayCurrencies,
    string PrimaryCurrency,
    ArgentinaProfileResponse? ArgentinaProfile);
