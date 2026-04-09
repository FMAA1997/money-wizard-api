using Domain.Models;

namespace Domain.Requests;

public sealed record CreateRecurrenceRequest(
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly? EndDate,
    int? TotalInstallments);
