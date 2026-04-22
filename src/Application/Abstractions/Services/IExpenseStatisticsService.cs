using Application.DTOs.Expense.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseStatisticsService
{
    Task<ErrorOr<ExpenseTotals>> GetTotals(int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<MonthlyExpenseStats>> GetMonthlyExpenseStats(int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<UpcomingExpense>> GetUpcoming(CancellationToken cancellationToken = default);
    Task<ErrorOr<MonthToMonthStats>> GetMonthToMonth(CancellationToken cancellationToken = default);
    Task<ErrorOr<SpendingByCategory>> GetSpendingByCategory(int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<CategoryDistribution>> GetCategoryDistribution(int year, int month, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseFlow>> GetExpenseFlow(int year, int? month, CancellationToken cancellationToken = default);
}
