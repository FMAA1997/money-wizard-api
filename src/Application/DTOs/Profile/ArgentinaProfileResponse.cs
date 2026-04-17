namespace Application.DTOs.Profile;

public sealed record ArgentinaProfileResponse(
    Guid Id,
    Guid UserId,
    string EmploymentStatus,
    Guid? MonotributoCategoryId);
