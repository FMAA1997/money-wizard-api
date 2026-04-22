using Domain.Models;

namespace Application.DTOs.Expense;

public sealed record ExpenseDetailResponse(
    Guid Id,
    Guid UserId,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    Guid? CategoryId,
    Guid? PaycheckId,
    Guid? InvoiceId,
    RecurrenceRule? RecurrenceRule,
    Guid? RecurringExpenseId,
    DateOnly? OriginalDate,
    bool IsDeleted,
    ExpenseCategory? Category,
    Domain.Models.Paycheck? Paycheck,
    Domain.Models.Invoice? Invoice,
    IReadOnlyDictionary<string, decimal> Amounts);
