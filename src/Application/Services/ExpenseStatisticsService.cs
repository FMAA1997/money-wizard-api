using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Expense.Statistics;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class ExpenseStatisticsService(
    IExpenseRepository expenseRepository,
    ICurrentUserProvider currentUserProvider) : IExpenseStatisticsService
{
    public async Task<ErrorOr<ExpenseTotals>> GetTotals(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearExpenses(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (expenses, previousYearStart, currentYearStart, currentYearEnd) = result.Value;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(expenses, currentYearStart, currentYearEnd);
        var totalYtd = SumExpandedAmounts(expenses, currentYearStart, ytdEnd);
        var totalPreviousYear = SumExpandedAmounts(expenses, previousYearStart, currentYearStart.AddDays(-1));

        var variationYoY = totalCurrentYear - totalPreviousYear;
        var variationYoyPctg = totalPreviousYear != 0
            ? (totalCurrentYear - totalPreviousYear) / totalPreviousYear * 100
            : 0;

        return new ExpenseTotals
        {
            TotalYTD = totalYtd,
            TotalCurrentYear = totalCurrentYear,
            TotalPreviousYear = totalPreviousYear,
            VariationYoY = variationYoY,
            VariationYoyPctg = variationYoyPctg
        };
    }

    public async Task<ErrorOr<MonthlyExpenseStats>> GetMonthlyExpenseStats(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearExpenses(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (expenses, previousYearStart, currentYearStart, currentYearEnd) = result.Value;

        var currentYearMonthly = GetMonthlyAmounts(expenses, currentYearStart, currentYearEnd);
        var previousYearMonthly = GetMonthlyAmounts(expenses, previousYearStart, currentYearStart.AddDays(-1));

        var avgMonthlyExpense = currentYearMonthly.Values.Sum() / 12;
        var previousYearAvgMonthlyExpense = previousYearMonthly.Values.Sum() / 12;

        var maxMonth = currentYearMonthly.MaxBy(kv => kv.Value);
        var minMonth = currentYearMonthly.MinBy(kv => kv.Value);

        return new MonthlyExpenseStats
        {
            AvgMonthlyExpense = avgMonthlyExpense,
            PreviousYearAvgMonthlyExpense = previousYearAvgMonthlyExpense,
            MaxMonthlyExpense = maxMonth.Value,
            MinMonthlyExpense = minMonth.Value,
            MaxMonthlyExpenseMonth = maxMonth.Key,
            MinMonthlyExpenseMonth = minMonth.Key
        };
    }

    public async Task<ErrorOr<UpcomingExpense>> GetUpcoming(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeEnd = today.AddYears(1);

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, today, rangeEnd, cancellationToken);

        var oneOffs = new List<Expense>();
        var series = new List<Expense>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Expense>();

        foreach (var e in expenses)
        {
            if (e.RecurrenceRule is not null)
                series.Add(e);
            else if (e.RecurringExpenseId.HasValue && e.OriginalDate.HasValue)
                exceptionLookup[(e.RecurringExpenseId.Value, e.OriginalDate.Value)] = e;
            else
                oneOffs.Add(e);
        }

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestDescription = null;

        foreach (var e in oneOffs.Where(e => e.Date >= today))
        {
            if (earliestDate is null || e.Date < earliestDate)
            {
                earliestDate = e.Date;
                earliestAmount = e.Amount;
                earliestDescription = e.Description;
            }
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, today, rangeEnd);
            if (occurrences.Count == 0) continue;

            var (date, _) = occurrences[0];

            if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
            {
                if (exception.IsDeleted) continue;
                if (earliestDate is null || date < earliestDate)
                {
                    earliestDate = date;
                    earliestAmount = exception.Amount;
                    earliestDescription = exception.Description;
                }
            }
            else if (earliestDate is null || date < earliestDate)
            {
                earliestDate = date;
                earliestAmount = s.Amount;
                earliestDescription = s.Description;
            }
        }

        if (earliestDate is null)
            return ExpenseErrors.NotFound;

        return new UpcomingExpense
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount
        };
    }

    public async Task<ErrorOr<SpendingByCategory>> GetSpendingByCategory(int year, CancellationToken cancellationToken = default)
    {
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, currentYearStart, currentYearEnd, cancellationToken);

        var monthlyByCategory = new Dictionary<(int Month, string Category), decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, categoryName, categoryColor) in ExpandWithCategory(expenses, currentYearStart, currentYearEnd))
        {
            var key = (date.Month, categoryName);
            monthlyByCategory[key] = monthlyByCategory.GetValueOrDefault(key) + amount;
            categoryColors[categoryName] = categoryColor;
        }

        var categories = categoryColors
            .Select(kv => new CategoryInfo { Name = kv.Key, Color = kv.Value })
            .OrderBy(c => c.Name)
            .ToList();

        var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var data = new List<Dictionary<string, object>>();

        for (var m = 1; m <= 12; m++)
        {
            var row = new Dictionary<string, object> { ["month"] = monthNames[m - 1] };
            foreach (var cat in categories)
            {
                row[cat.Name] = monthlyByCategory.GetValueOrDefault((m, cat.Name));
            }
            data.Add(row);
        }

        return new SpendingByCategory { Categories = categories, Data = data };
    }

    public async Task<ErrorOr<CategoryDistribution>> GetCategoryDistribution(int year, int month, CancellationToken cancellationToken = default)
    {
        if (month < 1 || month > 12)
            return Error.Validation("InvalidMonth", "Month must be between 1 and 12.");

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);

        var categoryTotals = new Dictionary<string, decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (_, amount, categoryName, categoryColor) in ExpandWithCategory(expenses, startDate, endDate))
        {
            categoryTotals[categoryName] = categoryTotals.GetValueOrDefault(categoryName) + amount;
            categoryColors[categoryName] = categoryColor;
        }

        var data = categoryTotals
            .Select(kv => new CategoryDistributionEntry
            {
                Name = kv.Key,
                Value = kv.Value,
                Fill = categoryColors[kv.Key]
            })
            .OrderByDescending(e => e.Value)
            .ToList();

        return new CategoryDistribution { Data = data };
    }

    public async Task<ErrorOr<MonthToMonthStats>> GetMonthToMonth(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);

        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, previousMonthStart, currentMonthEnd, cancellationToken);

        var currentMonth = SumExpandedAmounts(expenses, currentMonthStart, currentMonthEnd);
        var previousMonth = SumExpandedAmounts(expenses, previousMonthStart, previousMonthEnd);

        var variation = currentMonth - previousMonth;
        var variationPctg = previousMonth != 0
            ? (currentMonth - previousMonth) / previousMonth * 100
            : 0;

        return new MonthToMonthStats
        {
            CurrentMonth = currentMonth,
            PreviousMonth = previousMonth,
            Variation = variation,
            VariationPctg = variationPctg,
            CurrentMonthLabel = currentMonthStart.ToString("yyyy-MM"),
            PreviousMonthLabel = previousMonthStart.ToString("yyyy-MM")
        };
    }

    private async Task<ErrorOr<(IReadOnlyList<Expense> Expenses, DateOnly PreviousYearStart, DateOnly CurrentYearStart, DateOnly CurrentYearEnd)>>
        GetYearExpenses(int year, CancellationToken cancellationToken)
    {
        var previousYearStart = new DateOnly(year - 1, 1, 1);
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, previousYearStart, currentYearEnd, cancellationToken);

        return (expenses, previousYearStart, currentYearStart, currentYearEnd);
    }

    private static Dictionary<int, decimal> GetMonthlyAmounts(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate)
    {
        var monthly = new Dictionary<int, decimal>();

        var oneOffs = new List<Expense>();
        var series = new List<Expense>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Expense>();

        foreach (var e in expenses)
        {
            if (e.RecurrenceRule is not null)
                series.Add(e);
            else if (e.RecurringExpenseId.HasValue && e.OriginalDate.HasValue)
                exceptionLookup[(e.RecurringExpenseId.Value, e.OriginalDate.Value)] = e;
            else
                oneOffs.Add(e);
        }

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            var month = e.Date.Month;
            monthly[month] = monthly.GetValueOrDefault(month) + e.Amount;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amount;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amount = exception.Amount;
                }
                else
                {
                    amount = s.Amount;
                }

                var month = date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + amount;
            }
        }

        return monthly;
    }

    private static (List<Expense> OneOffs, List<Expense> Series, Dictionary<(Guid, DateOnly), Expense> ExceptionLookup)
        ClassifyExpenses(IReadOnlyList<Expense> expenses)
    {
        var oneOffs = new List<Expense>();
        var series = new List<Expense>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Expense>();

        foreach (var e in expenses)
        {
            if (e.RecurrenceRule is not null)
                series.Add(e);
            else if (e.RecurringExpenseId.HasValue && e.OriginalDate.HasValue)
                exceptionLookup[(e.RecurringExpenseId.Value, e.OriginalDate.Value)] = e;
            else
                oneOffs.Add(e);
        }

        return (oneOffs, series, exceptionLookup);
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string CategoryName, string CategoryColor)>
        ExpandWithCategory(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate)
    {
        const string uncategorizedName = "Uncategorized";
        const string uncategorizedColor = "#6b7280";

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            yield return (e.Date, e.Amount,
                e.Category?.Name ?? uncategorizedName,
                e.Category?.Color ?? uncategorizedColor);
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted) continue;
                    yield return (date, exception.Amount,
                        exception.Category?.Name ?? uncategorizedName,
                        exception.Category?.Color ?? uncategorizedColor);
                }
                else
                {
                    yield return (date, s.Amount,
                        s.Category?.Name ?? uncategorizedName,
                        s.Category?.Color ?? uncategorizedColor);
                }
            }
        }
    }

    private static decimal SumExpandedAmounts(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate)
    {
        var oneOffs = new List<Expense>();
        var series = new List<Expense>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Expense>();

        foreach (var e in expenses)
        {
            if (e.RecurrenceRule is not null)
                series.Add(e);
            else if (e.RecurringExpenseId.HasValue && e.OriginalDate.HasValue)
                exceptionLookup[(e.RecurringExpenseId.Value, e.OriginalDate.Value)] = e;
            else
                oneOffs.Add(e);
        }

        var total = oneOffs
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .Sum(e => e.Amount);

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (!exception.IsDeleted)
                        total += exception.Amount;
                }
                else
                {
                    total += s.Amount;
                }
            }
        }

        return total;
    }
}
