using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Invoice;
using Application.DTOs.Shared;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class InvoiceService(
    IInvoiceRepository invoiceRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork,
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext) : IInvoiceService
{
    public async Task<ErrorOr<IReadOnlyList<InvoiceDetailResponse>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var invoices = await invoiceRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return invoices.Select(i => MapEntityDetail(i, lookup, displayCurrencies)).ToList();
    }

    public async Task<ErrorOr<InvoiceDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceRepository.GetById(id, cancellationToken);
        if (invoice is null || invoice.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return MapEntityDetail(invoice, lookup, displayCurrencies);
    }

    public async Task<ErrorOr<Invoice>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var invoice = new Invoice
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Source = request.Source,
            RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
            {
                Frequency = request.Recurrence.Frequency,
                Interval = request.Recurrence.Interval,
                EndDate = request.Recurrence.EndDate,
                TotalInstallments = request.Recurrence.TotalInstallments
            }
        };

        await invoiceRepository.Add(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice;
    }

    public async Task<ErrorOr<Invoice>> Update(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceRepository.GetById(id, cancellationToken);
        if (invoice is null || invoice.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        invoice.Date = request.Date;
        invoice.Amount = request.Amount;
        invoice.Currency = request.Currency;
        invoice.Description = request.Description;
        invoice.Source = request.Source;
        invoice.RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
        {
            Frequency = request.Recurrence.Frequency,
            Interval = request.Recurrence.Interval,
            EndDate = request.Recurrence.EndDate,
            TotalInstallments = request.Recurrence.TotalInstallments
        };

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceRepository.GetById(id, cancellationToken);
        if (invoice is null || invoice.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        invoiceRepository.Delete(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<InvoiceResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdateInvoiceOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await invoiceRepository.GetException(id, date, cancellationToken);
        var occurrenceIndex = RecurrenceExpander.GetOccurrenceIndex(series.Date, series.RecurrenceRule, date);

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var currencyProfile = await userCurrencyContext.ResolveAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Date = request.Date ?? date;
            existing.Amount = request.Amount ?? series.Amount;
            existing.Currency = request.Currency ?? series.Currency;
            existing.Description = request.Description ?? series.Description;
            existing.Source = request.Source ?? series.Source;
            existing.IsDeleted = false;

            invoiceRepository.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapOverride(existing, series, occurrenceIndex, lookup, currencyProfile.DisplayCurrencies);
        }

        var exception = new Invoice
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date ?? date,
            Amount = request.Amount ?? series.Amount,
            Currency = request.Currency ?? series.Currency,
            Description = request.Description ?? series.Description,
            Source = request.Source ?? series.Source,
            RecurringInvoiceId = id,
            OriginalDate = date
        };

        await invoiceRepository.Add(exception, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapOverride(exception, series, occurrenceIndex, lookup, currencyProfile.DisplayCurrencies);
    }

    public async Task<ErrorOr<Invoice>> UpdateFromDate(Guid id, DateOnly date, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        if (request.Recurrence is not null && request.Recurrence.Interval < 1)
            return RecurrenceErrors.InvalidInterval;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);
        series.RecurrenceRule.EndDate = previousDate;

        if (previousDate is null)
            invoiceRepository.Delete(series);
        else
            invoiceRepository.Update(series);

        await invoiceRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        var recurrence = request.Recurrence;
        var newSeries = new Invoice
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Source = request.Source,
            RecurrenceRule = recurrence is null ? null : new RecurrenceRule
            {
                Frequency = recurrence.Frequency,
                Interval = recurrence.Interval,
                EndDate = recurrence.EndDate,
                TotalInstallments = recurrence.TotalInstallments
            }
        };

        await invoiceRepository.Add(newSeries, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return newSeries;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await invoiceRepository.GetException(id, date, cancellationToken);

        if (existing is not null)
        {
            existing.IsDeleted = true;
            invoiceRepository.Update(existing);
        }
        else
        {
            var exception = new Invoice
            {
                UserId = currentUserProvider.UserId,
                Date = date,
                Amount = series.Amount,
                Currency = series.Currency,
                Description = series.Description,
                RecurringInvoiceId = id,
                OriginalDate = date,
                IsDeleted = true
            };
            await invoiceRepository.Add(exception, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);

        await invoiceRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        if (previousDate is null)
        {
            invoiceRepository.Delete(series);
        }
        else
        {
            series.RecurrenceRule.EndDate = previousDate;
            invoiceRepository.Update(series);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<CalendarResponse<InvoiceCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var invoices = await invoiceRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);

        var occurrences = await ExpandOccurrences(invoices, startDate, endDate, cancellationToken);

        var invoiceLookup = invoices.ToDictionary(i => i.Id);

        var months = new List<string>();
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);
        while (current <= end)
        {
            months.Add(current.ToString("yyyy-MM"));
            current = current.AddMonths(1);
        }

        var rows = occurrences
            .GroupBy(o => o.RecurringInvoiceId ?? o.Id)
            .Select(g =>
            {
                var first = g.First();
                var seriesEntity = invoiceLookup.GetValueOrDefault(g.Key);
                var monthDict = g
                    .GroupBy(o => o.Date.ToString("yyyy-MM"))
                    .ToDictionary(
                        mg => mg.Key,
                        mg => (IReadOnlyList<InvoiceResponse>)[.. mg.OrderBy(o => o.Date)]);

                var source = seriesEntity?.Paycheck;

                return new InvoiceCalendarRow(
                    InvoiceId: g.Key,
                    Description: first.Description,
                    Source: source is null ? null : new InvoiceSourceInfo(source.Id, source.Description, source.Amount),
                    IsRecurring: first.IsRecurring,
                    Recurrence: first.Recurrence,
                    Occurrences: monthDict);
            })
            .ToList();

        var totals = months
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<InvoiceCalendarRow>(months, rows, totals);
    }

    private async Task<List<InvoiceResponse>> ExpandOccurrences(IReadOnlyList<Invoice> invoices, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
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

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var currencyProfile = await userCurrencyContext.ResolveAsync(cancellationToken);
        var displayCurrencies = currencyProfile.DisplayCurrencies;
        var results = new List<InvoiceResponse>();

        foreach (var oneOff in oneOffs)
        {
            results.Add(MapOneOff(oneOff, lookup, displayCurrencies));
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, index) in occurrences)
            {
                var key = (s.Id, date);
                if (exceptionLookup.TryGetValue(key, out var exception))
                {
                    if (exception.IsDeleted)
                        continue;

                    results.Add(MapOverride(exception, s, index, lookup, displayCurrencies));
                }
                else
                {
                    results.Add(MapVirtual(s, date, index, lookup, displayCurrencies));
                }
            }
        }

        results.Sort((a, b) => a.Date.CompareTo(b.Date));
        return results;
    }

    private static IReadOnlyDictionary<string, decimal> SumByCurrency(IEnumerable<InvoiceResponse> occurrences)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var occurrence in occurrences)
        {
            foreach (var (currency, amount) in occurrence.Amounts)
            {
                totals[currency] = totals.GetValueOrDefault(currency) + amount;
            }
        }
        return totals;
    }

    private static InvoiceDetailResponse MapEntityDetail(Invoice invoice, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: invoice.Id,
            UserId: invoice.UserId,
            Date: invoice.Date,
            Amount: invoice.Amount,
            Currency: invoice.Currency,
            Description: invoice.Description,
            Source: invoice.Source,
            RecurrenceRule: invoice.RecurrenceRule,
            RecurringInvoiceId: invoice.RecurringInvoiceId,
            OriginalDate: invoice.OriginalDate,
            IsDeleted: invoice.IsDeleted,
            Paycheck: invoice.Paycheck,
            Amounts: lookup.ConvertToAll(invoice.Amount, invoice.Currency, displayCurrencies, invoice.Date));

    private static InvoiceResponse MapOneOff(Invoice invoice, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: invoice.Id,
            Date: invoice.Date,
            Amount: invoice.Amount,
            Currency: invoice.Currency,
            Amounts: lookup.ConvertToAll(invoice.Amount, invoice.Currency, displayCurrencies, invoice.Date),
            Description: invoice.Description,
            Source: invoice.Source,
            IsRecurring: false,
            RecurringInvoiceId: null,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: null,
            InstallmentNumber: null,
            TotalInstallments: null);

    private static InvoiceResponse MapVirtual(Invoice series, DateOnly date, int occurrenceIndex, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: series.Id,
            Date: date,
            Amount: series.Amount,
            Currency: series.Currency,
            Amounts: lookup.ConvertToAll(series.Amount, series.Currency, displayCurrencies, date),
            Description: series.Description,
            Source: series.Source,
            IsRecurring: true,
            RecurringInvoiceId: series.Id,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: new RecurrenceInfo(
                series.Date,
                series.RecurrenceRule!.Frequency,
                series.RecurrenceRule.Interval,
                series.RecurrenceRule.EndDate,
                series.RecurrenceRule.TotalInstallments),
            InstallmentNumber: series.RecurrenceRule.TotalInstallments.HasValue ? occurrenceIndex + 1 : null,
            TotalInstallments: series.RecurrenceRule.TotalInstallments);

    private static InvoiceResponse MapOverride(Invoice exception, Invoice series, int occurrenceIndex, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: exception.Id,
            Date: exception.Date,
            Amount: exception.Amount,
            Currency: exception.Currency,
            Amounts: lookup.ConvertToAll(exception.Amount, exception.Currency, displayCurrencies, exception.Date),
            Description: exception.Description,
            Source: exception.Source,
            IsRecurring: true,
            RecurringInvoiceId: exception.RecurringInvoiceId,
            OriginalDate: exception.OriginalDate,
            IsOverride: true,
            Recurrence: new RecurrenceInfo(
                series.Date,
                series.RecurrenceRule!.Frequency,
                series.RecurrenceRule.Interval,
                series.RecurrenceRule.EndDate,
                series.RecurrenceRule.TotalInstallments),
            InstallmentNumber: series.RecurrenceRule.TotalInstallments.HasValue ? occurrenceIndex + 1 : null,
            TotalInstallments: series.RecurrenceRule.TotalInstallments);
}
