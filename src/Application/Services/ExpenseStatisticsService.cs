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

        var variationYoY = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => totalCurrentYear[c] - totalPreviousYear[c]);

        var variationYoyPctg = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => totalPreviousYear[c] != 0
                ? (totalCurrentYear[c] - totalPreviousYear[c]) / totalPreviousYear[c] * 100
                : 0m);

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

        var avgMonthlyExpense = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => currentYearMonthly.Values.Sum(m => m[c]) / 12);
        var previousYearAvgMonthlyExpense = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => previousYearMonthly.Values.Sum(m => m[c]) / 12);

        var maxMonthlyExpense = new Dictionary<string, decimal>();
        var minMonthlyExpense = new Dictionary<string, decimal>();
        var maxMonthlyExpenseMonth = new Dictionary<string, int>();
        var minMonthlyExpenseMonth = new Dictionary<string, int>();

        foreach (var c in scope.DisplayCurrencies)
        {
            if (currentYearMonthly.Count == 0)
            {
                maxMonthlyExpense[c] = 0m;
                minMonthlyExpense[c] = 0m;
                maxMonthlyExpenseMonth[c] = 0;
                minMonthlyExpenseMonth[c] = 0;
                continue;
            }

            var maxKv = currentYearMonthly.MaxBy(kv => kv.Value[c]);
            var minKv = currentYearMonthly.MinBy(kv => kv.Value[c]);
            maxMonthlyExpense[c] = maxKv.Value[c];
            minMonthlyExpense[c] = minKv.Value[c];
            maxMonthlyExpenseMonth[c] = maxKv.Key;
            minMonthlyExpenseMonth[c] = minKv.Key;
        }

        return new MonthlyExpenseStats
        {
            AvgMonthlyExpense = avgMonthlyExpense,
            PreviousYearAvgMonthlyExpense = previousYearAvgMonthlyExpense,
            MaxMonthlyExpense = maxMonthlyExpense,
            MinMonthlyExpense = minMonthlyExpense,
            MaxMonthlyExpenseMonth = maxMonthlyExpenseMonth,
            MinMonthlyExpenseMonth = minMonthlyExpenseMonth
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

        var monthlyByCategory = new Dictionary<(int Month, string Category), CurrencyTotals>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, currency, categoryName, categoryColor) in ExpandWithCategory(seriesList, currentYearStart, currentYearEnd))
        {
            var key = (date.Month, categoryName);
            if (!monthlyByCategory.TryGetValue(key, out var totals))
            {
                totals = scope.NewTotals();
                monthlyByCategory[key] = totals;
            }
            totals.Add(amount, currency, date);
            categoryColors[categoryName] = categoryColor;
        }

        var categories = categoryColors
            .Select(kv => new CategoryInfo { Name = kv.Key, Color = kv.Value })
            .OrderBy(c => c.Name)
            .ToList();

        var zeroPerCurrency = scope.DisplayCurrencies.ToDictionary(c => c, _ => 0m);
        var data = new List<Dictionary<string, object>>();

        for (var m = 1; m <= 12; m++)
        {
            var monthName = new DateOnly(year, m, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
            var row = new Dictionary<string, object> { ["month"] = monthName };
            var rowTotal = scope.NewTotals();
            foreach (var cat in categories)
            {
                if (monthlyByCategory.TryGetValue((m, cat.Name), out var totals))
                {
                    var dict = totals.ToDictionary();
                    row[cat.Name] = dict;
                    rowTotal.AddConverted(dict);
                }
                else
                {
                    row[cat.Name] = zeroPerCurrency;
                }
            }
            row["total"] = rowTotal.ToDictionary();
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

        var categoryTotals = new Dictionary<string, CurrencyTotals>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, currency, categoryName, categoryColor) in ExpandWithCategory(seriesList, startDate, endDate))
        {
            if (!categoryTotals.TryGetValue(categoryName, out var totals))
            {
                totals = scope.NewTotals();
                categoryTotals[categoryName] = totals;
            }
            totals.Add(amount, currency, date);
            categoryColors[categoryName] = categoryColor;
        }

        var data = categoryTotals
            .Select(kv => new CategoryDistributionEntry
            {
                Name = kv.Key,
                Value = kv.Value.ToDictionary(),
                Fill = categoryColors[kv.Key]
            })
            .OrderByDescending(e => e.Value.GetValueOrDefault(scope.PrimaryCurrency))
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

        var sourceCategoryTotals = new Dictionary<(string Source, string Category), CurrencyTotals>();
        var categoryExpenseTotals = new Dictionary<(string Category, string Description), CurrencyTotals>();
        var categoryColors = new Dictionary<string, string>();
        var sources = new HashSet<string>();
        var categories = new HashSet<string>();

        foreach (var (date, amount, currency, sourceName, categoryName, categoryColor, description) in ExpandWithSourceCategoryAndDescription(seriesList, startDate, endDate))
        {
            var scKey = (sourceName, categoryName);
            if (!sourceCategoryTotals.TryGetValue(scKey, out var scTotals))
            {
                scTotals = scope.NewTotals();
                sourceCategoryTotals[scKey] = scTotals;
            }
            scTotals.Add(amount, currency, date);

            var ceKey = (categoryName, description);
            if (!categoryExpenseTotals.TryGetValue(ceKey, out var ceTotals))
            {
                ceTotals = scope.NewTotals();
                categoryExpenseTotals[ceKey] = ceTotals;
            }
            ceTotals.Add(amount, currency, date);

            categoryColors[categoryName] = categoryColor;
            sources.Add(sourceName);
            categories.Add(categoryName);
        }

        const string otherSource = "Other";
        var orderedSources = sources
            .Where(s => s != otherSource)
            .OrderBy(s => s)
            .ToList();
        if (sources.Contains(otherSource))
            orderedSources.Add(otherSource);

        var orderedCategories = categories.OrderBy(c => c).ToList();
        var orderedExpenses = categoryExpenseTotals.Keys
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

        var sourceCategoryLinks = sourceCategoryTotals.Select(kv => new ExpenseFlowLink
        {
            Source = sourceIdx[kv.Key.Source],
            Target = categoryIdx[kv.Key.Category],
            Value = kv.Value.ToDictionary()
        });

        var categoryExpenseLinks = categoryExpenseTotals.Select(kv => new ExpenseFlowLink
        {
            Source = categoryIdx[kv.Key.Category],
            Target = expenseIdx[kv.Key],
            Value = kv.Value.ToDictionary()
        });

        var links = sourceCategoryLinks
            .Concat(categoryExpenseLinks)
            .Where(l => l.Value.GetValueOrDefault(scope.PrimaryCurrency) > 0)
            .OrderByDescending(l => l.Value.GetValueOrDefault(scope.PrimaryCurrency))
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

        var variation = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => currentMonth[c] - previousMonth[c]);
        var variationPctg = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => previousMonth[c] != 0
                ? (currentMonth[c] - previousMonth[c]) / previousMonth[c] * 100
                : 0m);

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

    private static Dictionary<int, IReadOnlyDictionary<string, decimal>> GetMonthlyAmounts(
        IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var monthly = new Dictionary<int, CurrencyTotals>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                var month = occurrence.Date.Month;
                if (!monthly.TryGetValue(month, out var totals))
                {
                    totals = scope.NewTotals();
                    monthly[month] = totals;
                }
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return monthly.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, decimal>)kv.Value.ToDictionary());
    }

    private static IReadOnlyDictionary<string, decimal> SumExpandedAmounts(
        IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return totals.ToDictionary();
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string Currency, string CategoryName, string CategoryColor)>
        ExpandWithCategory(IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate)
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
                yield return (occurrence.Date, amount, currency, categoryName, categoryColor);
            }
        }
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string Currency, string SourceName, string CategoryName, string CategoryColor, string Description)>
        ExpandWithSourceCategoryAndDescription(IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate)
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
                    amount,
                    currency,
                    source,
                    categoryName,
                    categoryColor,
                    series.Description);
            }
        }
    }
}
