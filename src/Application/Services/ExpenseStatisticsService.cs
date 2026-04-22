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
    ICurrentUserProvider currentUserProvider,
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext) : IExpenseStatisticsService
{
    public async Task<ErrorOr<ExpenseTotals>> GetTotals(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearExpenses(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (expenses, previousYearStart, currentYearStart, currentYearEnd) = result.Value;
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(expenses, currentYearStart, currentYearEnd, lookup, primaryCurrency);
        var totalYtd = SumExpandedAmounts(expenses, currentYearStart, ytdEnd, lookup, primaryCurrency);
        var totalPreviousYear = SumExpandedAmounts(expenses, previousYearStart, currentYearStart.AddDays(-1), lookup, primaryCurrency);

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
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var currentYearMonthly = GetMonthlyAmounts(expenses, currentYearStart, currentYearEnd, lookup, primaryCurrency);
        var previousYearMonthly = GetMonthlyAmounts(expenses, previousYearStart, currentYearStart.AddDays(-1), lookup, primaryCurrency);

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

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestCurrency = null;
        string? earliestDescription = null;

        foreach (var e in oneOffs.Where(e => e.Date >= today))
        {
            if (earliestDate is null || e.Date < earliestDate)
            {
                earliestDate = e.Date;
                earliestAmount = e.Amount;
                earliestCurrency = e.Currency;
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
                    earliestCurrency = exception.Currency;
                    earliestDescription = exception.Description;
                }
            }
            else if (earliestDate is null || date < earliestDate)
            {
                earliestDate = date;
                earliestAmount = s.Amount;
                earliestCurrency = s.Currency;
                earliestDescription = s.Description;
            }
        }

        if (earliestDate is null)
            return ExpenseErrors.NotFound;

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return new UpcomingExpense
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount,
            Currency = earliestCurrency!,
            Amounts = lookup.ConvertToAll(earliestAmount, earliestCurrency!, displayCurrencies, earliestDate.Value)
        };
    }

    public async Task<ErrorOr<SpendingByCategory>> GetSpendingByCategory(int year, CancellationToken cancellationToken = default)
    {
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, currentYearStart, currentYearEnd, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var monthlyByCategory = new Dictionary<(int Month, string Category), decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, categoryName, categoryColor) in ExpandWithCategory(expenses, currentYearStart, currentYearEnd, lookup, primaryCurrency))
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

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var categoryTotals = new Dictionary<string, decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (_, amount, categoryName, categoryColor) in ExpandWithCategory(expenses, startDate, endDate, lookup, primaryCurrency))
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

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var tripleTotals = new Dictionary<(string Source, string Category, string Description), decimal>();
        var categoryColors = new Dictionary<string, string>();
        var sources = new HashSet<string>();
        var categories = new HashSet<string>();
        var expenseKeys = new HashSet<(string Category, string Description)>();

        foreach (var (_, amount, sourceName, categoryName, categoryColor, description) in ExpandWithSourceCategoryAndDescription(expenses, startDate, endDate, lookup, primaryCurrency))
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

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, previousMonthStart, currentMonthEnd, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var currentMonth = SumExpandedAmounts(expenses, currentMonthStart, currentMonthEnd, lookup, primaryCurrency);
        var previousMonth = SumExpandedAmounts(expenses, previousMonthStart, previousMonthEnd, lookup, primaryCurrency);

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

    private static Dictionary<int, decimal> GetMonthlyAmounts(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var monthly = new Dictionary<int, decimal>();

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            var month = e.Date.Month;
            monthly[month] = monthly.GetValueOrDefault(month) + lookup.Convert(e.Amount, e.Currency, targetCurrency, e.Date);
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amountInUsd;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amountInUsd = lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date);
                }
                else
                {
                    amountInUsd = lookup.Convert(s.Amount, s.Currency, targetCurrency, date);
                }

                var month = date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + amountInUsd;
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
        ExpandWithCategory(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        const string uncategorizedName = "Uncategorized";
        const string uncategorizedColor = "#6b7280";

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            yield return (e.Date, lookup.Convert(e.Amount, e.Currency, targetCurrency, e.Date),
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
                    yield return (date, lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date),
                        exception.Category?.Name ?? uncategorizedName,
                        exception.Category?.Color ?? uncategorizedColor);
                }
                else
                {
                    yield return (date, lookup.Convert(s.Amount, s.Currency, targetCurrency, date),
                        s.Category?.Name ?? uncategorizedName,
                        s.Category?.Color ?? uncategorizedColor);
                }
            }
        }
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, string SourceName, string CategoryName, string CategoryColor, string Description)>
        ExpandWithSourceCategoryAndDescription(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        const string uncategorizedName = "Uncategorized";
        const string uncategorizedColor = "#6b7280";
        const string otherSource = "Other";

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            yield return (e.Date, lookup.Convert(e.Amount, e.Currency, targetCurrency, e.Date),
                e.Paycheck?.Description ?? e.Invoice?.Description ?? otherSource,
                e.Category?.Name ?? uncategorizedName,
                e.Category?.Color ?? uncategorizedColor,
                e.Description);
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted) continue;
                    yield return (date, lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date),
                        exception.Paycheck?.Description ?? exception.Invoice?.Description ?? otherSource,
                        exception.Category?.Name ?? uncategorizedName,
                        exception.Category?.Color ?? uncategorizedColor,
                        exception.Description);
                }
                else
                {
                    yield return (date, lookup.Convert(s.Amount, s.Currency, targetCurrency, date),
                        s.Paycheck?.Description ?? s.Invoice?.Description ?? otherSource,
                        s.Category?.Name ?? uncategorizedName,
                        s.Category?.Color ?? uncategorizedColor,
                        s.Description);
                }
            }
        }
    }

    private static decimal SumExpandedAmounts(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        var total = oneOffs
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .Sum(e => lookup.Convert(e.Amount, e.Currency, targetCurrency, e.Date));

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (!exception.IsDeleted)
                        total += lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date);
                }
                else
                {
                    total += lookup.Convert(s.Amount, s.Currency, targetCurrency, date);
                }
            }
        }

        return total;
    }
}
