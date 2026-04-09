using Domain.Models;

namespace Application.DTOs.Shared;

public sealed record RecurrenceInfo(
    DateOnly StartDate,
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly? EndDate,
    int? TotalInstallments);
