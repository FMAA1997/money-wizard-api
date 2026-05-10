using Application.DTOs.Shared;

namespace Application.DTOs.Expense;

public sealed record ExpenseResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    IReadOnlyDictionary<string, decimal> Amounts,
    string Description,
    Guid? CategoryId,
    Guid? PaycheckSeriesId,
    Guid? InvoiceSeriesId,
    bool IsRecurring,
    Guid? RecurringExpenseId,
    DateOnly? OriginalDate,
    bool IsOverride,
    RecurrenceInfo? Recurrence,
    int? InstallmentNumber,
    int? TotalInstallments);
