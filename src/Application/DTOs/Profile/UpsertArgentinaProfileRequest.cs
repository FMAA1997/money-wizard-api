namespace Application.DTOs.Profile;

public sealed record UpsertArgentinaProfileRequest(
    string EmploymentStatus,
    Guid? MonotributoCategoryId);
