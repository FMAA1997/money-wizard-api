using ErrorOr;

namespace Domain.Errors;

public static class ExpenseCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ExpenseCategory.NotFound", "Expense category not found.");
}
