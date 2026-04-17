using Application.DTOs.Shared;

namespace Application.DTOs.Expense;

public sealed record ExpenseResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid? CategoryId,
    Guid? PaycheckId,
    Guid? InvoiceId,
    bool IsRecurring,
    Guid? RecurringExpenseId,
    DateOnly? OriginalDate,
    bool IsOverride,
    RecurrenceInfo? Recurrence,
    int? InstallmentNumber,
    int? TotalInstallments);
