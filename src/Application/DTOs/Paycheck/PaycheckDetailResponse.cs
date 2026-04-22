using Domain.Models;

namespace Application.DTOs.Paycheck;

public sealed record PaycheckDetailResponse(
    Guid Id,
    Guid UserId,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    RecurrenceRule? RecurrenceRule,
    Guid? RecurringPaycheckId,
    DateOnly? OriginalDate,
    bool IsDeleted,
    IReadOnlyDictionary<string, decimal> Amounts);
