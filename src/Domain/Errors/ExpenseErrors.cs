using ErrorOr;

namespace Domain.Errors;

public static class ExpenseErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Expense.NotFound", "Expense not found.");

    public static readonly Error SegmentNotFound = Error.NotFound(
        "Expense.SegmentNotFound", "No expense segment covers the requested date.");
}
