using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Dashboard;
using Application.Services.Currency;
using Domain.Abstractions.Repositories;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class DashboardService(
    IPaycheckRepository paycheckRepository,
    IInvoiceRepository invoiceRepository,
    IExpenseRepository expenseRepository,
    ICurrentUserProvider currentUserProvider,
    ICurrencyConverter currencyConverter) : IDashboardService
{
    private const string OtherPaycheck = "Other Paycheck";
    private const string OtherInvoice = "Other Invoice";
    private const string Uncategorized = "Uncategorized";
    private const string UncategorizedColor = "#6b7280";
    private const string Savings = "Savings";

    public async Task<ErrorOr<MoneyFlow>> GetMoneyFlow(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return Error.Validation("InvalidRange", "'from' must be on or before 'to'.");

        var userId = currentUserProvider.UserId;

        var paycheckSeries = await paycheckRepository.GetByUserIdInRange(userId, from, to, cancellationToken);
        var invoiceSeriesList = await invoiceRepository.GetByUserIdInRange(userId, from, to, cancellationToken);
        var invoices = invoiceSeriesList; // alias used below
        var expenseSeriesList = await expenseRepository.GetByUserIdInRange(userId, from, to, cancellationToken);

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var paycheckTotals = ExpandPaycheckTotals(paycheckSeries, from, to, scope);
        var (invoiceTotals, invoiceSources) = ExpandInvoiceTotals(invoices, from, to, scope);

        var paycheckMeta = paycheckSeries.ToDictionary(s => s.Id, s => s.Description);
        var invoiceMeta = invoices.ToDictionary(s => s.Id, s => s.Description);

        var invoiceCategoryFlows = new Dictionary<(Guid InvoiceId, string Category), decimal>();
        var paycheckToOtherInvoice = new Dictionary<Guid, decimal>();
        var paycheckOtherInvoiceToCategory = new Dictionary<string, decimal>();
        var otherPaycheckToOtherInvoice = 0m;
        var otherOrphanToCategory = new Dictionary<string, decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, series, segment) in ExpandExpenseOccurrences(expenseSeriesList, from, to, scope))
        {
            var categoryName = series.Category?.Name ?? Uncategorized;
            var categoryColor = series.Category?.Color ?? UncategorizedColor;
            categoryColors[categoryName] = categoryColor;

            if (segment.InvoiceSeriesId is { } invoiceId && invoiceMeta.ContainsKey(invoiceId))
            {
                var key = (invoiceId, categoryName);
                invoiceCategoryFlows[key] = invoiceCategoryFlows.GetValueOrDefault(key) + amount;
            }
            else if (segment.PaycheckSeriesId is { } paycheckId && paycheckMeta.ContainsKey(paycheckId))
            {
                paycheckToOtherInvoice[paycheckId] = paycheckToOtherInvoice.GetValueOrDefault(paycheckId) + amount;
                paycheckOtherInvoiceToCategory[categoryName] = paycheckOtherInvoiceToCategory.GetValueOrDefault(categoryName) + amount;
            }
            else
            {
                otherPaycheckToOtherInvoice += amount;
                otherOrphanToCategory[categoryName] = otherOrphanToCategory.GetValueOrDefault(categoryName) + amount;
            }
        }

        var paycheckOutflowByInvoice = new Dictionary<(Guid PaycheckId, Guid InvoiceId), decimal>();
        var otherPaycheckOutflowByInvoice = new Dictionary<Guid, decimal>();
        foreach (var (invoiceId, total) in invoiceTotals)
        {
            if (total <= 0) continue;
            if (invoiceSources.TryGetValue(invoiceId, out var sourceId) && sourceId is { } pId && paycheckMeta.ContainsKey(pId))
                paycheckOutflowByInvoice[(pId, invoiceId)] = total;
            else
                otherPaycheckOutflowByInvoice[invoiceId] = total;
        }

        var paycheckOutflowTotals = new Dictionary<Guid, decimal>();
        foreach (var ((paycheckId, _), value) in paycheckOutflowByInvoice)
            paycheckOutflowTotals[paycheckId] = paycheckOutflowTotals.GetValueOrDefault(paycheckId) + value;
        foreach (var (paycheckId, value) in paycheckToOtherInvoice)
            paycheckOutflowTotals[paycheckId] = paycheckOutflowTotals.GetValueOrDefault(paycheckId) + value;

        var paycheckToSavings = new Dictionary<Guid, decimal>();
        foreach (var (paycheckId, total) in paycheckTotals)
        {
            var outflow = paycheckOutflowTotals.GetValueOrDefault(paycheckId);
            var remainder = total - outflow;
            if (remainder > 0)
                paycheckToSavings[paycheckId] = remainder;
        }

        var invoiceOutflowTotals = new Dictionary<Guid, decimal>();
        foreach (var ((invoiceId, _), value) in invoiceCategoryFlows)
            invoiceOutflowTotals[invoiceId] = invoiceOutflowTotals.GetValueOrDefault(invoiceId) + value;

        var invoiceToSavings = new Dictionary<Guid, decimal>();
        foreach (var (invoiceId, total) in invoiceTotals)
        {
            var remainder = total - invoiceOutflowTotals.GetValueOrDefault(invoiceId);
            if (remainder > 0)
                invoiceToSavings[invoiceId] = remainder;
        }

        return BuildMoneyFlow(
            paycheckMeta,
            invoiceMeta,
            paycheckOutflowByInvoice,
            otherPaycheckOutflowByInvoice,
            paycheckToOtherInvoice,
            otherPaycheckToOtherInvoice,
            invoiceCategoryFlows,
            paycheckOtherInvoiceToCategory,
            otherOrphanToCategory,
            paycheckToSavings,
            invoiceToSavings,
            categoryColors);
    }

    public async Task<ErrorOr<DashboardResults>> GetResults(CancellationToken cancellationToken = default)
    {
        var userId = currentUserProvider.UserId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var paycheckSeries = await paycheckRepository.GetByUserIdInRange(userId, yearStart, yearEnd, cancellationToken);
        var invoices = await invoiceRepository.GetByUserIdInRange(userId, yearStart, yearEnd, cancellationToken);
        var expenseSeriesList = await expenseRepository.GetByUserIdInRange(userId, yearStart, yearEnd, cancellationToken);
        var hasInvoices = await invoiceRepository.AnyForUser(userId, cancellationToken);

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        return new DashboardResults
        {
            CurrentMonth = BuildPeriod(paycheckSeries, invoices, expenseSeriesList, monthStart, monthEnd, hasInvoices, scope),
            YearToDate = BuildPeriod(paycheckSeries, invoices, expenseSeriesList, yearStart, today, hasInvoices, scope),
            YearProjected = BuildPeriod(paycheckSeries, invoices, expenseSeriesList, yearStart, yearEnd, hasInvoices, scope),
        };
    }

    private static DashboardResultPeriod BuildPeriod(
        IReadOnlyList<PaycheckSeries> paycheckSeries,
        IReadOnlyList<InvoiceSeries> invoices,
        IReadOnlyList<ExpenseSeries> expenseSeriesList,
        DateOnly from,
        DateOnly to,
        bool hasInvoices,
        CurrencyScope scope)
    {
        var paychecksDict = SumPaychecks(paycheckSeries, from, to, scope).ToDictionary();
        var expensesDict = SumExpenses(expenseSeriesList, from, to, scope).ToDictionary();

        if (!hasInvoices)
        {
            return new DashboardResultPeriod
            {
                TotalPaychecks = paychecksDict,
                TotalExpenses = expensesDict,
                PrimaryResult = Subtract(paychecksDict, expensesDict),
            };
        }

        var invoicedDict = SumInvoices(invoices, from, to, scope).ToDictionary();

        return new DashboardResultPeriod
        {
            TotalPaychecks = paychecksDict,
            TotalExpenses = expensesDict,
            TotalInvoiced = invoicedDict,
            PrimaryResult = Subtract(invoicedDict, expensesDict),
            NonInvoicedTotal = Subtract(paychecksDict, invoicedDict),
            FinalResult = Subtract(paychecksDict, expensesDict),
        };
    }

    private static CurrencyTotals SumPaychecks(IReadOnlyList<PaycheckSeries> seriesList, DateOnly from, DateOnly to, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, from, to))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return totals;
    }

    private static CurrencyTotals SumExpenses(IReadOnlyList<ExpenseSeries> seriesList, DateOnly from, DateOnly to, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, from, to))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return totals;
    }

    private static CurrencyTotals SumInvoices(IReadOnlyList<InvoiceSeries> seriesList, DateOnly from, DateOnly to, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            var sign = Sign(series.Type);
            foreach (var occurrence in InvoiceSeriesExpander.Expand(series, from, to))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                totals.Add(sign * amount, currency, occurrence.Date);
            }
        }
        return totals;
    }

    private static IReadOnlyDictionary<string, decimal> Subtract(
        IReadOnlyDictionary<string, decimal> a,
        IReadOnlyDictionary<string, decimal> b)
    {
        var result = new Dictionary<string, decimal>(a.Count);
        foreach (var (currency, value) in a)
            result[currency] = value - b.GetValueOrDefault(currency);
        return result;
    }

    private static MoneyFlow BuildMoneyFlow(
        Dictionary<Guid, string> paycheckMeta,
        Dictionary<Guid, string> invoiceMeta,
        Dictionary<(Guid PaycheckId, Guid InvoiceId), decimal> paycheckToInvoice,
        Dictionary<Guid, decimal> otherPaycheckToInvoice,
        Dictionary<Guid, decimal> paycheckToOtherInvoice,
        decimal otherPaycheckToOtherInvoice,
        Dictionary<(Guid InvoiceId, string Category), decimal> invoiceToCategory,
        Dictionary<string, decimal> paycheckOtherInvoiceToCategory,
        Dictionary<string, decimal> otherOrphanToCategory,
        Dictionary<Guid, decimal> paycheckToSavings,
        Dictionary<Guid, decimal> invoiceToSavings,
        Dictionary<string, string> categoryColors)
    {
        var usedPaycheckIds = new HashSet<Guid>();
        foreach (var ((pId, _), _) in paycheckToInvoice) usedPaycheckIds.Add(pId);
        foreach (var (pId, _) in paycheckToOtherInvoice) usedPaycheckIds.Add(pId);
        foreach (var (pId, _) in paycheckToSavings) usedPaycheckIds.Add(pId);

        var useOtherPaycheck = otherPaycheckToInvoice.Count > 0 || otherPaycheckToOtherInvoice > 0;

        var usedInvoiceIds = new HashSet<Guid>();
        foreach (var ((_, iId), _) in paycheckToInvoice) usedInvoiceIds.Add(iId);
        foreach (var (iId, _) in otherPaycheckToInvoice) usedInvoiceIds.Add(iId);
        foreach (var ((iId, _), _) in invoiceToCategory) usedInvoiceIds.Add(iId);
        foreach (var (iId, _) in invoiceToSavings) usedInvoiceIds.Add(iId);

        var useOtherInvoice = paycheckToOtherInvoice.Count > 0
            || otherPaycheckToOtherInvoice > 0
            || paycheckOtherInvoiceToCategory.Count > 0
            || otherOrphanToCategory.Count > 0;

        var categoryTotals = new Dictionary<string, decimal>();
        foreach (var ((_, cat), v) in invoiceToCategory)
            categoryTotals[cat] = categoryTotals.GetValueOrDefault(cat) + v;
        foreach (var (cat, v) in paycheckOtherInvoiceToCategory)
            categoryTotals[cat] = categoryTotals.GetValueOrDefault(cat) + v;
        foreach (var (cat, v) in otherOrphanToCategory)
            categoryTotals[cat] = categoryTotals.GetValueOrDefault(cat) + v;

        var savingsTotal = paycheckToSavings.Values.Sum() + invoiceToSavings.Values.Sum();

        var nodes = new List<MoneyFlowNode>();
        var paycheckIdx = new Dictionary<Guid, int>();
        var invoiceIdx = new Dictionary<Guid, int>();
        var categoryIdx = new Dictionary<string, int>();
        int otherPaycheckIdx = -1;
        int otherInvoiceIdx = -1;
        int savingsIdx = -1;

        foreach (var pId in usedPaycheckIds.OrderBy(id => paycheckMeta.GetValueOrDefault(id, string.Empty)))
        {
            paycheckIdx[pId] = nodes.Count;
            nodes.Add(new MoneyFlowNode { Name = paycheckMeta.GetValueOrDefault(pId, string.Empty), Kind = "paycheck" });
        }
        if (useOtherPaycheck)
        {
            otherPaycheckIdx = nodes.Count;
            nodes.Add(new MoneyFlowNode { Name = OtherPaycheck, Kind = "paycheck" });
        }

        foreach (var iId in usedInvoiceIds.OrderBy(id => invoiceMeta.GetValueOrDefault(id, string.Empty)))
        {
            invoiceIdx[iId] = nodes.Count;
            nodes.Add(new MoneyFlowNode { Name = invoiceMeta.GetValueOrDefault(iId, string.Empty), Kind = "invoice" });
        }
        if (useOtherInvoice)
        {
            otherInvoiceIdx = nodes.Count;
            nodes.Add(new MoneyFlowNode { Name = OtherInvoice, Kind = "invoice" });
        }

        var orderedCategories = categoryTotals.Keys
            .Where(c => c != Uncategorized)
            .OrderBy(c => c)
            .ToList();
        if (categoryTotals.ContainsKey(Uncategorized))
            orderedCategories.Add(Uncategorized);

        foreach (var cat in orderedCategories)
        {
            categoryIdx[cat] = nodes.Count;
            nodes.Add(new MoneyFlowNode
            {
                Name = cat,
                Kind = "category",
                Color = categoryColors.GetValueOrDefault(cat, UncategorizedColor)
            });
        }

        if (savingsTotal > 0)
        {
            savingsIdx = nodes.Count;
            nodes.Add(new MoneyFlowNode { Name = Savings, Kind = "savings" });
        }

        var links = new List<MoneyFlowLink>();

        foreach (var ((pId, iId), value) in paycheckToInvoice)
            links.Add(new MoneyFlowLink { Source = paycheckIdx[pId], Target = invoiceIdx[iId], Value = value });

        foreach (var (iId, value) in otherPaycheckToInvoice)
            links.Add(new MoneyFlowLink { Source = otherPaycheckIdx, Target = invoiceIdx[iId], Value = value });

        foreach (var (pId, value) in paycheckToOtherInvoice)
            links.Add(new MoneyFlowLink { Source = paycheckIdx[pId], Target = otherInvoiceIdx, Value = value });

        if (otherPaycheckToOtherInvoice > 0)
            links.Add(new MoneyFlowLink { Source = otherPaycheckIdx, Target = otherInvoiceIdx, Value = otherPaycheckToOtherInvoice });

        foreach (var ((iId, cat), value) in invoiceToCategory)
            links.Add(new MoneyFlowLink { Source = invoiceIdx[iId], Target = categoryIdx[cat], Value = value });

        foreach (var (cat, value) in paycheckOtherInvoiceToCategory)
            links.Add(new MoneyFlowLink { Source = otherInvoiceIdx, Target = categoryIdx[cat], Value = value });

        foreach (var (cat, value) in otherOrphanToCategory)
        {
            var existing = links.FindIndex(l => l.Source == otherInvoiceIdx && l.Target == categoryIdx[cat]);
            if (existing >= 0)
                links[existing].Value += value;
            else
                links.Add(new MoneyFlowLink { Source = otherInvoiceIdx, Target = categoryIdx[cat], Value = value });
        }

        foreach (var (pId, value) in paycheckToSavings)
            links.Add(new MoneyFlowLink { Source = paycheckIdx[pId], Target = savingsIdx, Value = value });

        foreach (var (iId, value) in invoiceToSavings)
            links.Add(new MoneyFlowLink { Source = invoiceIdx[iId], Target = savingsIdx, Value = value });

        var filteredLinks = links
            .Where(l => l.Value > 0)
            .OrderByDescending(l => l.Value)
            .ToList();

        return new MoneyFlow { Nodes = nodes, Links = filteredLinks };
    }

    private static Dictionary<Guid, decimal> ExpandPaycheckTotals(
        IReadOnlyList<PaycheckSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var totals = new Dictionary<Guid, decimal>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                totals[series.Id] = totals.GetValueOrDefault(series.Id) + scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }
        }
        return totals;
    }

    private static (Dictionary<Guid, decimal> Totals, Dictionary<Guid, Guid?> Sources) ExpandInvoiceTotals(
        IReadOnlyList<InvoiceSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var totals = new Dictionary<Guid, decimal>();
        var sources = new Dictionary<Guid, Guid?>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var series in seriesList)
        {
            var sign = Sign(series.Type);
            foreach (var occurrence in InvoiceSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                totals[series.Id] = totals.GetValueOrDefault(series.Id) + sign * scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }

            var activeSegment = InvoiceSeriesExpander.GetActiveSegment(series, today);
            sources[series.Id] = activeSegment?.Source;
        }

        return (totals, sources);
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, ExpenseSeries Series, ExpenseSegment Segment)> ExpandExpenseOccurrences(
        IReadOnlyList<ExpenseSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        foreach (var series in seriesList)
        {
            foreach (var occurrence in ExpenseSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment.Currency;
                yield return (occurrence.Date, scope.ConvertToPrimary(amount, currency, occurrence.Date), series, occurrence.Segment);
            }
        }
    }

    private static decimal Sign(InvoiceType type) =>
        type == InvoiceType.CreditNote ? -1m : 1m;
}
