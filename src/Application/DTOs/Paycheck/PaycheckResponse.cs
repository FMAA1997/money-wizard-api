using Application.DTOs.Shared;

namespace Application.DTOs.Paycheck;

public sealed record PaycheckResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    bool IsRecurring,
    Guid? RecurringPaycheckId,
    DateOnly? OriginalDate,
    bool IsOverride,
    RecurrenceInfo? Recurrence,
    int? InstallmentNumber,
    int? TotalInstallments);
