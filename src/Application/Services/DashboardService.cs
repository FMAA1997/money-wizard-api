using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Dashboard;
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
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext) : IDashboardService
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

        var paychecks = await paycheckRepository.GetByUserIdInRange(userId, from, to, cancellationToken);
        var invoices = await invoiceRepository.GetByUserIdInRange(userId, from, to, cancellationToken);
        var expenses = await expenseRepository.GetByUserIdInRange(userId, from, to, cancellationToken);

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var paycheckTotals = ExpandPaycheckTotals(paychecks, from, to, lookup, primaryCurrency);
        var (invoiceTotals, invoiceSources) = ExpandInvoiceTotals(invoices, from, to, lookup, primaryCurrency);

        var paycheckMeta = paychecks
            .Where(p => p.RecurringPaycheckId is null)
            .ToDictionary(p => p.Id, p => p.Description);
        var invoiceMeta = invoices
            .Where(i => i.RecurringInvoiceId is null)
            .ToDictionary(i => i.Id, i => i.Description);

        var invoiceCategoryFlows = new Dictionary<(Guid InvoiceId, string Category), decimal>();
        var paycheckToOtherInvoice = new Dictionary<Guid, decimal>();
        var paycheckOtherInvoiceToCategory = new Dictionary<string, decimal>();
        var otherPaycheckToOtherInvoice = 0m;
        var otherOrphanToCategory = new Dictionary<string, decimal>();
        var categoryColors = new Dictionary<string, string>();

        foreach (var (date, amount, expense) in ExpandExpenseOccurrences(expenses, from, to, lookup, primaryCurrency))
        {
            var categoryName = expense.Category?.Name ?? Uncategorized;
            var categoryColor = expense.Category?.Color ?? UncategorizedColor;
            categoryColors[categoryName] = categoryColor;

            if (expense.InvoiceId is { } invoiceId && invoiceMeta.ContainsKey(invoiceId))
            {
                var key = (invoiceId, categoryName);
                invoiceCategoryFlows[key] = invoiceCategoryFlows.GetValueOrDefault(key) + amount;
            }
            else if (expense.PaycheckId is { } paycheckId && paycheckMeta.ContainsKey(paycheckId))
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
        IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var totals = new Dictionary<Guid, decimal>();
        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        foreach (var p in oneOffs.Where(p => p.Date >= startDate && p.Date <= endDate))
            totals[p.Id] = totals.GetValueOrDefault(p.Id) + lookup.Convert(p.Amount, p.Currency, targetCurrency, p.Date);

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);
            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted) continue;
                    totals[s.Id] = totals.GetValueOrDefault(s.Id) + lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date);
                }
                else
                {
                    totals[s.Id] = totals.GetValueOrDefault(s.Id) + lookup.Convert(s.Amount, s.Currency, targetCurrency, date);
                }
            }
        }

        return totals;
    }

    private static (Dictionary<Guid, decimal> Totals, Dictionary<Guid, Guid?> Sources) ExpandInvoiceTotals(
        IReadOnlyList<Invoice> invoices, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var totals = new Dictionary<Guid, decimal>();
        var sources = new Dictionary<Guid, Guid?>();
        var (oneOffs, series, exceptionLookup) = ClassifyInvoices(invoices);

        foreach (var i in oneOffs.Where(i => i.Date >= startDate && i.Date <= endDate))
        {
            var sign = Sign(i.Type);
            totals[i.Id] = totals.GetValueOrDefault(i.Id) + sign * lookup.Convert(i.Amount, i.Currency, targetCurrency, i.Date);
            sources[i.Id] = i.Source;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);
            var sign = Sign(s.Type);
            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted) continue;
                    totals[s.Id] = totals.GetValueOrDefault(s.Id) + sign * lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date);
                }
                else
                {
                    totals[s.Id] = totals.GetValueOrDefault(s.Id) + sign * lookup.Convert(s.Amount, s.Currency, targetCurrency, date);
                }
            }
            sources[s.Id] = s.Source;
        }

        return (totals, sources);
    }

    private static IEnumerable<(DateOnly Date, decimal Amount, Expense Expense)> ExpandExpenseOccurrences(
        IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var (oneOffs, series, exceptionLookup) = ClassifyExpenses(expenses);

        foreach (var e in oneOffs.Where(e => e.Date >= startDate && e.Date <= endDate))
            yield return (e.Date, lookup.Convert(e.Amount, e.Currency, targetCurrency, e.Date), e);

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);
            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted) continue;
                    yield return (date, lookup.Convert(exception.Amount, exception.Currency, targetCurrency, exception.Date), exception);
                }
                else
                {
                    yield return (date, lookup.Convert(s.Amount, s.Currency, targetCurrency, date), s);
                }
            }
        }
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

    private static decimal Sign(InvoiceType type) =>
        type == InvoiceType.CreditNote ? -1m : 1m;
}
