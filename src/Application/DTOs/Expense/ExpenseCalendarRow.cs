using Application.DTOs.Shared;

namespace Application.DTOs.Expense;

public sealed record ExpenseCalendarRow(
    Guid ExpenseId,
    string Description,
    ExpenseCategoryInfo? Category,
    ExpenseSourceInfo? Source,
    bool IsRecurring,
    RecurrenceInfo? Recurrence,
    IReadOnlyDictionary<string, IReadOnlyList<ExpenseResponse>> Occurrences);

public sealed record ExpenseCategoryInfo(
    Guid Id,
    string Name,
    string Color);

public sealed record ExpenseSourceInfo(
    Guid Id,
    string Description,
    decimal Amount);
