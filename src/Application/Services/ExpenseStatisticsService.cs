using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Expense.Statistics;
using Application.Services.Currency;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class ExpenseStatisticsService(
    IExpenseRepository expenseRepository,
    ICurrentUserProvider currentUserProvider,
    ICurrencyConverter currencyConverter) : IExpenseStatisticsService
{
    public async Task<ErrorOr<ExpenseTotals>> GetTotals(int year, CancellationToken cancellationToken = default)
    {
        var (seriesList, previousYearStart, currentYearStart, currentYearEnd) =
            await GetYearSeries(year, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(seriesList, currentYearStart, currentYearEnd, scope);
        var totalYtd = SumExpandedAmounts(seriesList, currentYearStart, ytdEnd, scope);
        var totalPreviousYear = SumExpandedAmounts(seriesList, previousYearStart, currentYearStart.AddDays(-1), scope);

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
        var (seriesList, previousYearStart, currentYearStart, currentYearEnd) =
            await GetYearSeries(year, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var currentYearMonthly = GetMonthlyAmounts(seriesList, currentYearStart, currentYearEnd, scope);
        var previousYearMonthly = GetMonthlyAmounts(seriesList, previousYearStart, currentYearStart.AddDays(-1), scope);

        var avgMonthlyExpense = currentYearMonthly.Values.Sum() / 12;
        var previousYearAvgMonthlyExpense = previousYearMonthly.Values.Sum() / 12;

        var maxMonth = currentYearMonthly.Count > 0 ? currentYearMonthly.MaxBy(kv => kv.Value) : default;
        var minMonth = currentYearMonthly.Count > 0 ? currentYearMonthly.MinBy(kv => kv.Value) : default;

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

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, today, rangeEnd, cancellationToken);

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestCurrency = null;
        string? earliestDescription = null;

        foreach (var series in seriesList)
        {
            var occurrences = ExpenseSeriesExpander.Expand(series, today, rangeEnd);
            if (occurrences.Count == 0) continue;

            var first = occurrences[0];
            if (earliestDate is null || first.Date < earliestDate)
            {
                earliestDate = first.Date;
                earliestAmount = first.Exception?.Amount ?? first.Segment!.Amount;
                earliestCurrency = first.Exception?.Currency ?? first.Segment!.Currency;
                earliestDescription = series.Description;
            }
        }

        if (earliestDate is null)
            return ExpenseErrors.NotFound;

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        return new UpcomingExpense
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount,
            Currency = earliestCurrency!,
            Amounts = scope.ConvertToDisplay(earliestAmount, earliestCurrency!, earliestDate.Value)
        };
    }

    public async Task<ErrorOr<SpendingByCategory>> GetSpendingByCategory(int year, CancellationToken cancellationToken = default)
    {
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, currentYearStart, currentYearEnd, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var monthlyByCategory = new Dictionary<(int Month, string Category), decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, categoryName, categoryColor) in ExpandWithCategory(seriesList, currentYearStart, currentYearEnd, scope))
        {
            var key = (date.Month, categoryName);
            monthlyByCategory[key] = monthlyByCategory.GetValueOrDefault(key) + amount;
            categoryColors[categoryName] = categoryColor;
        }

        var categories = categoryColors
            .Select(kv => new CategoryInfo { Name = kv.Key, Color = kv.Value })
            .OrderBy(c => c.Name)
            .ToList();

        var data = new List<Dictionary<string, object>>();

        for (var m = 1; m <= 12; m++)
        {
            var monthName = new DateOnly(year, m, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
            var row = new Dictionary<string, object> { ["month"] = monthName };
            decimal total = 0;
            foreach (var cat in categories)
            {
                var amount = monthlyByCategory.GetValueOrDefault((m, cat.Name));
                row[cat.Name] = amount;
                total += amount;
            }
            row["total"] = total;
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

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var categoryTotals = new Dictionary<string, decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (_, amount, categoryName, categoryColor) in ExpandWithCategory(seriesList, startDate, endDate, scope))
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

    public async Task<ErrorOr<ExpenseFlow>> GetExpenseFlow(int year, int? month, CancellationToken cancellationToken = default)
    {
        if (month.HasValue && (month.Value < 1 || month.Value > 12))
            return Error.Validation("InvalidMonth", "Month must be between 1 and 12.");

        DateOnly startDate;
        DateOnly endDate;
        if (month.HasValue)
        {
            startDate = new DateOnly(year, month.Value, 1);
            endDate = startDate.AddMonths(1).AddDays(-1);
        }
        else
        {
            startDate = new DateOnly(year, 1, 1);
            endDate = new DateOnly(year, 12, 31);
        }

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var tripleTotals = new Dictionary<(string Source, string Category, string Description), decimal>();
        var categoryColors = new Dictionary<string, string>();
        var sources = new HashSet<string>();
        var categories = new HashSet<string>();
        var expenseKeys = new HashSet<(string Category, string Description)>();

        foreach (var (_, amount, sourceName, categoryName, categoryColor, description) in ExpandWithSourceCategoryAndDescription(seriesList, startDate, endDate, scope))
        {
            var key = (sourceName, categoryName, description);
            tripleTotals[key] = tripleTotals.GetValueOrDefault(key) + amount;
            categoryColors[categoryName] = categoryColor;
            sources.Add(sourceName);
            categories.Add(categoryName);
            expenseKeys.Add((categoryName, description));
        }

        const string otherSource = "Other";
        var orderedSources = sources
            .Where(s => s != otherSource)
            .OrderBy(s => s)
            .ToList();
        if (sources.Contains(otherSource))
            orderedSources.Add(otherSource);

        var orderedCategories = categories.OrderBy(c => c).ToList();
        var orderedExpenses = expenseKeys
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Description)
            .ToList();

        var nodes = new List<ExpenseFlowNode>();
        var sourceIdx = new Dictionary<string, int>();
        var categoryIdx = new Dictionary<string, int>();
        var expenseIdx = new Dictionary<(string Category, string Description), int>();

        foreach (var s in orderedSources)
        {
            sourceIdx[s] = nodes.Count;
            nodes.Add(new ExpenseFlowNode { Name = s, Kind = "source", Color = null });
        }
        foreach (var c in orderedCategories)
        {
            categoryIdx[c] = nodes.Count;
            nodes.Add(new ExpenseFlowNode { Name = c, Kind = "category", Color = categoryColors[c] });
        }
        foreach (var (cat, desc) in orderedExpenses)
        {
            expenseIdx[(cat, desc)] = nodes.Count;
            nodes.Add(new ExpenseFlowNode { Name = desc, Kind = "expense", Color = categoryColors[cat] });
        }

        var sourceCategoryLinks = tripleTotals
            .GroupBy(kv => (kv.Key.Source, kv.Key.Category))
            .Select(g => new ExpenseFlowLink
            {
                Source = sourceIdx[g.Key.Source],
                Target = categoryIdx[g.Key.Category],
                Value = g.Sum(x => x.Value)
            });

        var categoryExpenseLinks = tripleTotals
            .GroupBy(kv => (kv.Key.Category, kv.Key.Description))
            .Select(g => new ExpenseFlowLink
            {
                Source = categoryIdx[g.Key.Category],
                Target = expenseIdx[(g.Key.Category, g.Key.Description)],
                Value = g.Sum(x => x.Value)
            });

        var links = sourceCategoryLinks
            .Concat(categoryExpenseLinks)
            .Where(l => l.Value > 0)
            .OrderByDescending(l => l.Value)
            .ToList();

        return new ExpenseFlow { Nodes = nodes, Links = links };
    }

    public async Task<ErrorOr<MonthToMonthStats>> GetMonthToMonth(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);

        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, previousMonthStart, currentMonthEnd, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var currentMonth = SumExpandedAmounts(seriesList, currentMonthStart, currentMonthEnd, scope);
        var previousMonth = SumExpandedAmounts(seriesList, previousMonthStart, previousMonthEnd, scope);

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

    private async Task<(IReadOnlyList<ExpenseSeries> SeriesList, DateOnly PreviousYearStart, DateOnly CurrentYearStart, DateOnly CurrentYearEnd)>
        GetYearSeries(int year, CancellationToken cancellationToken)
    {
        var previousYearStart = new DateOnly(year - 1, 1, 1);
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, previousYearStart, currentYearEnd, cancellationToken);

        return (seriesList, previousYearStart, currentYearStart, currentYearEnd);
    }

    private static Dictionary<int, decimal> GetMonthlyAmounts(
        IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var monthly = new Dictionary<int, decimal>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                var month = occurrence.Date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }
        }
        return monthly;
    }

    private static decimal SumExpandedAmounts(
        IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        decimal total = 0;
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                total += scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }
        }
        return total;
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string CategoryName, string CategoryColor)>
        ExpandWithCategory(IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        const string uncategorizedName = "Uncategorized";
        const string uncategorizedColor = "#6b7280";

        foreach (var series in seriesList)
        {
            var categoryName = series.Category?.Name ?? uncategorizedName;
            var categoryColor = series.Category?.Color ?? uncategorizedColor;

            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                yield return (occurrence.Date, scope.ConvertToPrimary(amount, currency, occurrence.Date), categoryName, categoryColor);
            }
        }
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string SourceName, string CategoryName, string CategoryColor, string Description)>
        ExpandWithSourceCategoryAndDescription(IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        const string uncategorizedName = "Uncategorized";
        const string uncategorizedColor = "#6b7280";
        const string otherSource = "Other";

        foreach (var series in seriesList)
        {
            var categoryName = series.Category?.Name ?? uncategorizedName;
            var categoryColor = series.Category?.Color ?? uncategorizedColor;

            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                var source = occurrence.Segment?.PaycheckSeries?.Description
                    ?? occurrence.Segment?.InvoiceSeries?.Description
                    ?? otherSource;

                yield return (
                    occurrence.Date,
                    scope.ConvertToPrimary(amount, currency, occurrence.Date),
                    source,
                    categoryName,
                    categoryColor,
                    series.Description);
            }
        }
    }
}
