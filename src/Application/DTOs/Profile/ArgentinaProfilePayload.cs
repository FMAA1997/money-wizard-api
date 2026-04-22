namespace Application.DTOs.Profile;

public sealed record ArgentinaProfilePayload(
    string EmploymentStatus,
    Guid? MonotributoCategoryId);
