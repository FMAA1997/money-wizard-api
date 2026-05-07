using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.DTOs.Investment.Statistics;
using Application.Services.Currency;
using Domain.Abstractions.Repositories;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class InvestmentStatisticsService(
    IInvestmentService investmentService,
    ICurrencyConverter currencyConverter,
    IExpenseRepository expenseRepository,
    IPaycheckRepository paycheckRepository,
    IInvoiceRepository invoiceRepository,
    IInvestmentYieldCalculator yieldCalculator,
    ICurrentUserProvider currentUserProvider) : IInvestmentStatisticsService
{
    public async Task<ErrorOr<AssetClassDistribution>> GetAssetClassDistribution(CancellationToken cancellationToken = default)
    {
        var portfolioResult = await investmentService.GetPortfolio(cancellationToken);
        if (portfolioResult.IsError)
            return portfolioResult.Errors;

        var portfolio = portfolioResult.Value;
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var data = portfolio.ByAssetClass
            .Select(b => new AssetClassDistributionEntry(
                AssetClass: b.AssetClass,
                Value: b.CurrentValue,
                Percentage: b.WeightPct))
            .OrderByDescending(e => e.Value.TryGetValue(scope.PrimaryCurrency, out var v) ? v : 0m)
            .ToList();

        return new AssetClassDistribution(data, portfolio.UnvaluedCount);
    }

    public async Task<ErrorOr<CurrencyDistribution>> GetCurrencyDistribution(CancellationToken cancellationToken = default)
    {
        var valuationsResult = await investmentService.GetAll(cancellationToken);
        if (valuationsResult.IsError)
            return valuationsResult.Errors;

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var totals = scope.NewTotals();
        var perCurrency = new Dictionary<string, CurrencyTotals>(StringComparer.OrdinalIgnoreCase);
        var unvalued = 0;

        foreach (var v in valuationsResult.Value)
        {
            if (v.ValuationStatus != ValuationStatus.Live || v.CurrentValue is null || v.CurrentCurrency is null)
            {
                unvalued++;
                continue;
            }

            totals.AddConverted(v.CurrentValue);

            if (!perCurrency.TryGetValue(v.CurrentCurrency, out var currencyTotals))
            {
                currencyTotals = scope.NewTotals();
                perCurrency[v.CurrentCurrency] = currencyTotals;
            }
            currencyTotals.AddConverted(v.CurrentValue);
        }

        var portfolioTotal = totals.ToDictionary();
        var data = perCurrency
            .Select(kvp =>
            {
                var currencyTotal = kvp.Value.ToDictionary();
                return new CurrencyDistributionEntry(
                    Currency: kvp.Key,
                    Value: currencyTotal,
                    Percentage: ComputeWeights(currencyTotal, portfolioTotal));
            })
            .OrderByDescending(e => e.Value.TryGetValue(scope.PrimaryCurrency, out var val) ? val : 0m)
            .ToList();

        return new CurrencyDistribution(data, unvalued);
    }

    public async Task<ErrorOr<MonthsOfExpensesCovered>> GetMonthsOfExpensesCovered(CancellationToken cancellationToken = default)
    {
        var portfolioResult = await investmentService.GetPortfolio(cancellationToken);
        if (portfolioResult.IsError)
            return portfolioResult.Errors;

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var (ttmStart, ttmEnd, today) = TtmWindow();

        var expenses = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, ttmStart, ttmEnd, cancellationToken);

        var totals = scope.NewTotals();
        var earliestOccurrence = AccumulateExpenses(expenses, ttmStart, ttmEnd, totals);

        var months = earliestOccurrence is null
            ? 0
            : Math.Min(12, MonthsBetween(earliestOccurrence.Value, today));

        var summed = totals.ToDictionary();
        var avgMonthly = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => months == 0 ? 0m : summed[c] / months);

        var totalInvestments = portfolioResult.Value.TotalCurrentValue;

        var monthsCovered = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c =>
            {
                if (months == 0 || avgMonthly[c] == 0m)
                    return (decimal?)null;
                var total = totalInvestments.TryGetValue(c, out var t) ? t : 0m;
                return total / avgMonthly[c];
            });

        return new MonthsOfExpensesCovered(monthsCovered, totalInvestments, avgMonthly, months);
    }

    public async Task<ErrorOr<FinancialIndependence>> GetFinancialIndependence(CancellationToken cancellationToken = default)
    {
        var valuationsResult = await investmentService.GetAll(cancellationToken);
        if (valuationsResult.IsError)
            return valuationsResult.Errors;

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var annualYield = yieldCalculator.ComputeAnnualYield(valuationsResult.Value, scope);
        var totalInvested = SumValuations(valuationsResult.Value, scope);

        var (ttmStart, ttmEnd, today) = TtmWindow();
        var userId = currentUserProvider.UserId;

        var expenses = await expenseRepository.GetByUserIdInRange(userId, ttmStart, ttmEnd, cancellationToken);
        var paychecks = await paycheckRepository.GetByUserIdInRange(userId, ttmStart, ttmEnd, cancellationToken);
        var invoices = await invoiceRepository.GetByUserIdInRange(userId, ttmStart, ttmEnd, cancellationToken);

        var expenseTotals = scope.NewTotals();
        var earliestExpense = AccumulateExpenses(expenses, ttmStart, ttmEnd, expenseTotals);

        var paycheckTotals = scope.NewTotals();
        var earliestPaycheck = AccumulatePaychecks(paychecks, ttmStart, ttmEnd, paycheckTotals);

        var invoiceTotals = scope.NewTotals();
        var earliestInvoice = AccumulateInvoices(invoices, ttmStart, ttmEnd, invoiceTotals);

        var earliest = Earliest(earliestExpense, earliestPaycheck, earliestInvoice);
        var months = earliest is null ? 0 : Math.Min(12, MonthsBetween(earliest.Value, today));
        var annualizationFactor = months == 0 ? 0m : 12m / months;

        var ttmExpenses = expenseTotals.ToDictionary();
        var ttmPaychecks = paycheckTotals.ToDictionary();
        var ttmInvoices = invoiceTotals.ToDictionary();

        var annualExpenses = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => ttmExpenses[c] * annualizationFactor);

        var annualIncome = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => (ttmPaychecks[c] + ttmInvoices[c]) * annualizationFactor);

        var annualSavings = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => annualIncome[c] - annualExpenses[c]);

        return new FinancialIndependence(
            AnnualYield: annualYield,
            Savings: BuildMilestone(annualYield, totalInvested, annualSavings, scope),
            Expenses: BuildMilestone(annualYield, totalInvested, annualExpenses, scope),
            Income: BuildMilestone(annualYield, totalInvested, annualIncome, scope),
            Months: months);
    }

    private static FinancialIndependenceMilestone BuildMilestone(
        IReadOnlyDictionary<string, decimal> annualYield,
        IReadOnlyDictionary<string, decimal> totalInvested,
        IReadOnlyDictionary<string, decimal> target,
        CurrencyScope scope)
    {
        var current = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => annualYield.GetValueOrDefault(c));
        var coverage = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c =>
            {
                var t = target[c];
                if (t <= 0m)
                    return (decimal?)null;
                return current[c] / t;
            });
        var requiredInvestments = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c =>
            {
                var t = target[c];
                if (t <= 0m)
                    return (decimal?)null;
                var yieldAmount = annualYield.GetValueOrDefault(c);
                var invested = totalInvested.GetValueOrDefault(c);
                if (yieldAmount <= 0m || invested <= 0m)
                    return null;
                return t * invested / yieldAmount;
            });
        return new FinancialIndependenceMilestone(current, target, coverage, requiredInvestments);
    }

    private static IReadOnlyDictionary<string, decimal> SumValuations(
        IReadOnlyList<InvestmentDetailResponse> valuations,
        CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var v in valuations)
        {
            if (v.CurrentValue is not null)
                totals.AddConverted(v.CurrentValue);
        }
        return totals.ToDictionary();
    }

    private static (DateOnly TtmStart, DateOnly TtmEnd, DateOnly Today) TtmWindow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ttmStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var ttmEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        return (ttmStart, ttmEnd, today);
    }

    private static DateOnly? Earliest(params DateOnly?[] candidates)
    {
        DateOnly? result = null;
        foreach (var c in candidates)
        {
            if (c is null) continue;
            if (result is null || c < result) result = c;
        }
        return result;
    }

    private static DateOnly? AccumulateExpenses(
        IReadOnlyList<Expense> expenses,
        DateOnly startDate,
        DateOnly endDate,
        CurrencyTotals totals)
    {
        DateOnly? earliest = null;

        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            totals.Add(e.Amount, e.Currency, e.Date);
            if (earliest is null || e.Date < earliest)
                earliest = e.Date;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amount;
                string currency;
                DateOnly conversionDate;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amount = exception.Amount;
                    currency = exception.Currency;
                    conversionDate = exception.Date;
                }
                else
                {
                    amount = s.Amount;
                    currency = s.Currency;
                    conversionDate = date;
                }

                totals.Add(amount, currency, conversionDate);
                if (earliest is null || date < earliest)
                    earliest = date;
            }
        }

        return earliest;
    }

    private static DateOnly? AccumulatePaychecks(
        IReadOnlyList<Paycheck> paychecks,
        DateOnly startDate,
        DateOnly endDate,
        CurrencyTotals totals)
    {
        DateOnly? earliest = null;

        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        foreach (var p in oneOffs.Where(p => p.Date >= startDate && p.Date <= endDate))
        {
            totals.Add(p.Amount, p.Currency, p.Date);
            if (earliest is null || p.Date < earliest)
                earliest = p.Date;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amount;
                string currency;
                DateOnly conversionDate;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amount = exception.Amount;
                    currency = exception.Currency;
                    conversionDate = exception.Date;
                }
                else
                {
                    amount = s.Amount;
                    currency = s.Currency;
                    conversionDate = date;
                }

                totals.Add(amount, currency, conversionDate);
                if (earliest is null || date < earliest)
                    earliest = date;
            }
        }

        return earliest;
    }

    private static DateOnly? AccumulateInvoices(
        IReadOnlyList<Invoice> invoices,
        DateOnly startDate,
        DateOnly endDate,
        CurrencyTotals totals)
    {
        DateOnly? earliest = null;

        var (oneOffs, series, exceptionLookup) = ClassifyInvoices(invoices);

        foreach (var i in oneOffs.Where(i => i.Date >= startDate && i.Date <= endDate))
        {
            var sign = SignOf(i.Type);
            totals.Add(sign * i.Amount, i.Currency, i.Date);
            if (earliest is null || i.Date < earliest)
                earliest = i.Date;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);
            var sign = SignOf(s.Type);

            foreach (var (date, _) in occurrences)
            {
                decimal amount;
                string currency;
                DateOnly conversionDate;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amount = exception.Amount;
                    currency = exception.Currency;
                    conversionDate = exception.Date;
                }
                else
                {
                    amount = s.Amount;
                    currency = s.Currency;
                    conversionDate = date;
                }

                totals.Add(sign * amount, currency, conversionDate);
                if (earliest is null || date < earliest)
                    earliest = date;
            }
        }

        return earliest;
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

    private static (List<Paycheck> OneOffs, List<Paycheck> Series, Dictionary<(Guid, DateOnly), Paycheck> ExceptionLookup)
        ClassifyPaychecks(IReadOnlyList<Paycheck> paychecks)
    {
        var oneOffs = new List<Paycheck>();
        var series = new List<Paycheck>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Paycheck>();

        foreach (var p in paychecks)
        {
            if (p.RecurrenceRule is not null)
                series.Add(p);
            else if (p.RecurringPaycheckId.HasValue && p.OriginalDate.HasValue)
                exceptionLookup[(p.RecurringPaycheckId.Value, p.OriginalDate.Value)] = p;
            else
                oneOffs.Add(p);
        }

        return (oneOffs, series, exceptionLookup);
    }

    private static (List<Invoice> OneOffs, List<Invoice> Series, Dictionary<(Guid, DateOnly), Invoice> ExceptionLookup)
        ClassifyInvoices(IReadOnlyList<Invoice> invoices)
    {
        var oneOffs = new List<Invoice>();
        var series = new List<Invoice>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Invoice>();

        foreach (var i in invoices)
        {
            if (i.RecurrenceRule is not null)
                series.Add(i);
            else if (i.RecurringInvoiceId.HasValue && i.OriginalDate.HasValue)
                exceptionLookup[(i.RecurringInvoiceId.Value, i.OriginalDate.Value)] = i;
            else
                oneOffs.Add(i);
        }

        return (oneOffs, series, exceptionLookup);
    }

    private static decimal SignOf(InvoiceType type) =>
        type == InvoiceType.CreditNote ? -1m : 1m;

    private static int MonthsBetween(DateOnly earliest, DateOnly today) =>
        (today.Year * 12 + today.Month) - (earliest.Year * 12 + earliest.Month) + 1;

    private static IReadOnlyDictionary<string, decimal> ComputeWeights(
        IReadOnlyDictionary<string, decimal> partTotal,
        IReadOnlyDictionary<string, decimal> portfolioTotal)
    {
        var weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (currency, total) in portfolioTotal)
        {
            weights[currency] = total == 0m ? 0m : 100m * partTotal.GetValueOrDefault(currency) / total;
        }
        return weights;
    }
}
