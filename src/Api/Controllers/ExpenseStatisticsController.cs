using Application.Abstractions.Services;
using Application.DTOs.Expense.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/expenses/statistics")]
[Authorize]
public sealed class ExpenseStatisticsController(IExpenseStatisticsService expenseStatisticsService) : ErrorController
{
    [HttpGet("totals")]
    public async Task<ActionResult<ExpenseTotals>> GetTotals([FromQuery] int? year = null, CancellationToken cancellationToken = default) =>
        MatchOk(await expenseStatisticsService.GetTotals(
            year ?? DateTime.UtcNow.Year,
            cancellationToken
        ));

    [HttpGet("monthly-expense")]
    public async Task<ActionResult<MonthlyExpenseStats>> GetMonthlyExpenseStats([FromQuery] int? year = null, CancellationToken cancellationToken = default) =>
        MatchOk(await expenseStatisticsService.GetMonthlyExpenseStats(
            year ?? DateTime.UtcNow.Year,
            cancellationToken
        ));

    [HttpGet("upcoming")]
    public async Task<ActionResult<UpcomingExpense>> GetUpcoming(CancellationToken cancellationToken) =>
        MatchOk(await expenseStatisticsService.GetUpcoming(
            cancellationToken
        ));

    [HttpGet("month-to-month")]
    public async Task<ActionResult<MonthToMonthStats>> GetMonthToMonth(CancellationToken cancellationToken) =>
        MatchOk(await expenseStatisticsService.GetMonthToMonth(
            cancellationToken
        ));

    [HttpGet("spending-by-category")]
    public async Task<ActionResult<SpendingByCategory>> GetSpendingByCategory([FromQuery] int? year = null, CancellationToken cancellationToken = default) =>
        MatchOk(await expenseStatisticsService.GetSpendingByCategory(
            year ?? DateTime.UtcNow.Year,
            cancellationToken
        ));

    [HttpGet("category-distribution")]
    public async Task<ActionResult<CategoryDistribution>> GetCategoryDistribution([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken = default) =>
        MatchOk(await expenseStatisticsService.GetCategoryDistribution(
            year,
            month,
            cancellationToken
        ));
}
