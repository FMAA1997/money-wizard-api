using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Invoice;
using Application.DTOs.Paycheck;
using Application.DTOs.Shared;
using Application.Services.Currency;
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
    ICurrencyConverter currencyConverter) : IInvoiceService
{
    public async Task<ErrorOr<IReadOnlyList<InvoiceDetailResponse>>> GetAllInRange(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await invoiceRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);

        return seriesList.Select(MapDetail).ToList();
    }

    public async Task<ErrorOr<IReadOnlyList<InvoiceResponse>>> GetOccurrences(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await invoiceRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var results = new List<InvoiceResponse>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in InvoiceSeriesExpander.Expand(series, startDate, endDate))
                results.Add(MapOccurrence(occurrence, series, scope));
        }

        return results.OrderBy(o => o.Date).ToList();
    }

    public async Task<ErrorOr<InvoiceDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        return MapDetail(series);
    }

    public async Task<ErrorOr<InvoiceSeries>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        unitOfWork.BeginTransaction();

        var parentResolution = await ResolveAndMaterializeParent(
            request.Type, request.ParentInvoiceSeriesId, request.ParentOriginalDate, cancellationToken);
        if (parentResolution.IsError)
        {
            await unitOfWork.RollbackAsync();
            return parentResolution.Errors;
        }

        var series = new InvoiceSeries
        {
            UserId = currentUserProvider.UserId,
            Description = request.Description,
            Type = request.Type,
            Class = request.Class,
            PointOfSale = request.PointOfSale,
            BaseNumber = request.BaseNumber,
            ParentExceptionId = parentResolution.Value?.Id,
            Segments =
            {
                new InvoiceSegment
                {
                    EffectiveFrom = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    Source = request.Source,
                    RecurrenceRule = MapRecurrence(request.Recurrence)
                }
            }
        };

        await invoiceRepository.Add(series, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<InvoiceSeries>> Update(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        series.Description = request.Description;
        series.Class = request.Class;
        series.PointOfSale = request.PointOfSale;
        series.BaseNumber = request.BaseNumber;

        invoiceRepository.Update(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        invoiceRepository.Delete(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<InvoiceResponse>> UpdateOccurrence(
        Guid id, DateOnly date, UpdateInvoiceOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        var existing = await invoiceRepository.GetException(series.Id, date, cancellationToken);
        var isRecurrence = InvoiceSeriesExpander.IsRecurrenceOccurrence(series, date);

        if (!isRecurrence && existing is null)
        {
            if (request.Amount is null || string.IsNullOrEmpty(request.Currency))
                return RecurrenceErrors.InsertionRequiresAmountAndCurrency;
            if (request.Number is null)
                return InvoiceErrors.InsertionRequiresNumber;
        }

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        InvoiceException exception;
        InvoiceSegment? segment;
        int occurrenceIndex;
        int globalIndex;

        if (existing is not null && !existing.OriginalDate.HasValue)
        {
            existing.Date = date;
            existing.Amount = request.Amount;
            existing.Currency = request.Currency;
            existing.Number = request.Number;
            invoiceRepository.UpdateException(existing);
            exception = existing;
            segment = null;
            occurrenceIndex = -1;
            globalIndex = -1;
        }
        else if (existing is not null)
        {
            existing.Date = request.Date;
            existing.Amount = request.Amount;
            existing.Currency = request.Currency;
            existing.IsDeleted = false;
            // Preserve frozen Number on overrides.
            invoiceRepository.UpdateException(existing);
            exception = existing;
            segment = InvoiceSeriesExpander.GetSegmentForDate(series, date)!;
            occurrenceIndex = segment.RecurrenceRule is null
                ? 0
                : RecurrenceExpander.GetOccurrenceIndex(segment.EffectiveFrom, segment.RecurrenceRule, date);
            globalIndex = InvoiceSeriesExpander.GetGlobalOccurrenceIndex(series, date);
        }
        else if (isRecurrence)
        {
            globalIndex = InvoiceSeriesExpander.GetGlobalOccurrenceIndex(series, date);
            long? frozenNumber = series.BaseNumber + globalIndex;

            exception = new InvoiceException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                Date = request.Date,
                Amount = request.Amount,
                Currency = request.Currency,
                Number = frozenNumber,
                IsDeleted = false
            };
            await invoiceRepository.AddException(exception, cancellationToken);
            segment = InvoiceSeriesExpander.GetSegmentForDate(series, date)!;
            occurrenceIndex = segment.RecurrenceRule is null
                ? 0
                : RecurrenceExpander.GetOccurrenceIndex(segment.EffectiveFrom, segment.RecurrenceRule, date);
        }
        else
        {
            exception = new InvoiceException
            {
                SeriesId = series.Id,
                OriginalDate = null,
                Date = date,
                Amount = request.Amount,
                Currency = request.Currency,
                Number = request.Number,
                IsDeleted = false
            };
            await invoiceRepository.AddException(exception, cancellationToken);
            segment = null;
            occurrenceIndex = -1;
            globalIndex = -1;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var occurrence = new InvoiceOccurrence(
            Date: exception.Date ?? date,
            OriginalDate: exception.OriginalDate,
            Segment: segment,
            Exception: exception,
            OccurrenceIndex: occurrenceIndex,
            GlobalIndex: globalIndex,
            Number: exception.Number);

        return MapOccurrence(occurrence, series, scope);
    }

    public async Task<ErrorOr<InvoiceSeries>> UpdateFromDate(
        Guid id, DateOnly date, UpdateInvoiceFromDateRequest request, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        if (!InvoiceSeriesExpander.IsRecurrenceOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var segment = InvoiceSeriesExpander.GetSegmentForDate(series, date)!;

        unitOfWork.BeginTransaction();
        CapOrDeleteSegment(segment, date);

        await invoiceRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
        await invoiceRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);

        var newSegment = new InvoiceSegment
        {
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = request.Amount,
            Currency = request.Currency,
            Source = request.Source,
            RecurrenceRule = MapRecurrence(request.Recurrence)
        };

        await invoiceRepository.AddSegment(newSegment, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return (await invoiceRepository.GetById(series.Id, cancellationToken))!;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        var existing = await invoiceRepository.GetException(series.Id, date, cancellationToken);

        if (existing is not null && !existing.OriginalDate.HasValue)
        {
            invoiceRepository.DeleteException(existing);
        }
        else if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.Date = null;
            existing.Amount = null;
            existing.Currency = null;
            // Preserve frozen Number for fiscal record (even though the occurrence is skipped).
            invoiceRepository.UpdateException(existing);
        }
        else if (InvoiceSeriesExpander.IsRecurrenceOccurrence(series, date))
        {
            var exception = new InvoiceException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                IsDeleted = true
            };
            await invoiceRepository.AddException(exception, cancellationToken);
        }
        else
        {
            return InvoiceErrors.NotFound;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await invoiceRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return InvoiceErrors.NotFound;

        if (!InvoiceSeriesExpander.IsRecurrenceOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var segment = InvoiceSeriesExpander.GetSegmentForDate(series, date)!;

        unitOfWork.BeginTransaction();
        CapOrDeleteSegment(segment, date);

        await invoiceRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
        await invoiceRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        var refreshed = await invoiceRepository.GetById(series.Id, cancellationToken);
        if (refreshed is not null && refreshed.Segments.Count == 0)
        {
            invoiceRepository.Delete(refreshed);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Deleted;
    }

    public async Task<ErrorOr<CalendarResponse<InvoiceCalendarRow>>> GetCalendar(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await invoiceRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var months = new List<string>();
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);
        while (current <= end)
        {
            months.Add(current.ToString("yyyy-MM"));
            current = current.AddMonths(1);
        }

        var rows = new List<InvoiceCalendarRow>();
        foreach (var series in seriesList)
        {
            var occurrences = InvoiceSeriesExpander.Expand(series, startDate, endDate);
            if (occurrences.Count == 0) continue;

            var responses = occurrences
                .Select(o => MapOccurrence(o, series, scope))
                .OrderBy(r => r.Date)
                .ToList();

            var monthDict = responses
                .GroupBy(r => r.Date.ToString("yyyy-MM"))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<InvoiceResponse>)[.. g.OrderBy(o => o.Date)]);

            var activeSegment = InvoiceSeriesExpander.GetActiveSegment(series, today);
            var isRecurring = series.Segments.Any(s => s.RecurrenceRule is not null);
            var recurrence = activeSegment?.RecurrenceRule is null
                ? null
                : new RecurrenceInfo(
                    activeSegment.EffectiveFrom,
                    activeSegment.RecurrenceRule.Frequency,
                    activeSegment.RecurrenceRule.Interval,
                    activeSegment.RecurrenceRule.EndDate,
                    activeSegment.RecurrenceRule.TotalInstallments);

            var source = activeSegment?.PaycheckSeries is not null
                ? new InvoiceSourceInfo(activeSegment.PaycheckSeries.Id, activeSegment.PaycheckSeries.Description, 0m)
                : null;

            rows.Add(new InvoiceCalendarRow(
                InvoiceId: series.Id,
                Description: series.Description,
                Type: series.Type,
                ParentExceptionId: series.ParentExceptionId,
                Class: series.Class,
                PointOfSale: series.PointOfSale,
                Source: source,
                IsRecurring: isRecurring,
                Recurrence: recurrence,
                Occurrences: monthDict));
        }

        var totals = months
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<InvoiceCalendarRow>(months, rows, totals);
    }

    private async Task<ErrorOr<InvoiceException?>> ResolveAndMaterializeParent(
        InvoiceType type,
        Guid? parentSeriesId,
        DateOnly? parentOriginalDate,
        CancellationToken cancellationToken)
    {
        if (type == InvoiceType.Invoice)
        {
            if (parentSeriesId.HasValue)
                return InvoiceErrors.ParentNotAllowed;
            return (InvoiceException?)null;
        }

        if (!parentSeriesId.HasValue || !parentOriginalDate.HasValue)
            return InvoiceErrors.ParentRequired;

        var parent = await invoiceRepository.GetById(parentSeriesId.Value, cancellationToken);
        if (parent is null || parent.UserId != currentUserProvider.UserId)
            return InvoiceErrors.ParentNotFound;

        if (parent.Type != InvoiceType.Invoice)
            return InvoiceErrors.ParentMustBeInvoice;

        if (!InvoiceSeriesExpander.IsExistingOccurrence(parent, parentOriginalDate.Value))
            return InvoiceErrors.ParentOccurrenceNotValid;

        var existing = await invoiceRepository.GetException(parent.Id, parentOriginalDate.Value, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
                return InvoiceErrors.ParentOccurrenceNotValid;
            return existing;
        }

        // No row exists yet → date must be a recurrence-generated occurrence; materialize an override to freeze Number.
        var globalIndex = InvoiceSeriesExpander.GetGlobalOccurrenceIndex(parent, parentOriginalDate.Value);
        long? frozenNumber = parent.BaseNumber + globalIndex;

        var materialized = new InvoiceException
        {
            SeriesId = parent.Id,
            OriginalDate = parentOriginalDate.Value,
            Date = null,
            Amount = null,
            Currency = null,
            Number = frozenNumber,
            IsDeleted = false
        };
        await invoiceRepository.AddException(materialized, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return materialized;
    }

    private void CapOrDeleteSegment(InvoiceSegment segment, DateOnly boundary)
    {
        if (segment.RecurrenceRule is null)
        {
            invoiceRepository.DeleteSegment(segment);
            return;
        }

        var previous = RecurrenceExpander.GetPreviousOccurrence(segment.EffectiveFrom, segment.RecurrenceRule, boundary);
        if (previous is null)
        {
            invoiceRepository.DeleteSegment(segment);
        }
        else
        {
            segment.RecurrenceRule.EndDate = previous;
            invoiceRepository.UpdateSegment(segment);
        }
    }

    private static RecurrenceRule? MapRecurrence(CreateRecurrenceRequest? request) =>
        request is null
            ? null
            : new RecurrenceRule
            {
                Frequency = request.Frequency,
                Interval = request.Interval,
                EndDate = request.EndDate,
                TotalInstallments = request.TotalInstallments
            };

    private static IReadOnlyDictionary<string, decimal> SumByCurrency(IEnumerable<InvoiceResponse> occurrences)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var occurrence in occurrences)
        {
            var sign = occurrence.Type == InvoiceType.CreditNote ? -1m : 1m;
            foreach (var (currency, amount) in occurrence.Amounts)
            {
                totals[currency] = totals.GetValueOrDefault(currency) + sign * amount;
            }
        }
        return totals;
    }

    private static InvoiceDetailResponse MapDetail(InvoiceSeries series)
    {
        InvoiceParentSummary? parent = null;
        if (series.ParentException is not null)
        {
            parent = new InvoiceParentSummary(
                SeriesId: series.ParentException.SeriesId,
                OriginalDate: series.ParentException.OriginalDate,
                Number: series.ParentException.Number,
                Description: series.ParentException.Series?.Description ?? string.Empty);
        }

        return new InvoiceDetailResponse(
            Id: series.Id,
            UserId: series.UserId,
            Description: series.Description,
            Type: series.Type,
            Class: series.Class,
            PointOfSale: series.PointOfSale,
            BaseNumber: series.BaseNumber,
            ParentExceptionId: series.ParentExceptionId,
            ParentInvoice: parent,
            Segments: series.Segments
                .OrderBy(s => s.EffectiveFrom)
                .Select(s => new InvoiceSegmentResponse(
                    Id: s.Id,
                    EffectiveFrom: s.EffectiveFrom,
                    Amount: s.Amount,
                    Currency: s.Currency,
                    RecurrenceRule: s.RecurrenceRule,
                    Source: s.Source,
                    PaycheckSeries: s.PaycheckSeries is null
                        ? null
                        : new PaycheckSummary(s.PaycheckSeries.Id, s.PaycheckSeries.Description)))
                .ToList(),
            Exceptions: series.Exceptions
                .OrderBy(e => e.OriginalDate)
                .Select(e => new InvoiceExceptionResponse(
                    Id: e.Id,
                    OriginalDate: e.OriginalDate,
                    Date: e.Date,
                    Amount: e.Amount,
                    Currency: e.Currency,
                    Number: e.Number,
                    IsDeleted: e.IsDeleted))
                .ToList());
    }

    private static InvoiceResponse MapOccurrence(InvoiceOccurrence occurrence, InvoiceSeries series, CurrencyScope scope)
    {
        var segment = occurrence.Segment;
        var amount = occurrence.Exception?.Amount ?? segment!.Amount;
        var currency = occurrence.Exception?.Currency ?? segment!.Currency;
        var date = occurrence.Date;
        var rule = segment?.RecurrenceRule;
        var hasInstallments = rule?.TotalInstallments is not null;
        var isOverride = occurrence.Exception is not null && occurrence.OriginalDate.HasValue;

        return new InvoiceResponse(
            Id: occurrence.Exception?.Id ?? BuildOccurrenceId(series.Id, occurrence.OriginalDate!.Value),
            Date: date,
            Amount: amount,
            Currency: currency,
            Amounts: scope.ConvertToDisplay(amount, currency, date),
            Description: series.Description,
            Source: segment?.Source,
            Type: series.Type,
            ParentExceptionId: series.ParentExceptionId,
            Class: series.Class,
            PointOfSale: series.PointOfSale,
            Number: occurrence.Number,
            IsRecurring: rule is not null,
            RecurringInvoiceId: rule is not null ? series.Id : null,
            OriginalDate: isOverride ? occurrence.OriginalDate : null,
            IsOverride: isOverride,
            Recurrence: segment is null || rule is null
                ? null
                : new RecurrenceInfo(
                    segment.EffectiveFrom,
                    rule.Frequency,
                    rule.Interval,
                    rule.EndDate,
                    rule.TotalInstallments),
            InstallmentNumber: hasInstallments ? occurrence.OccurrenceIndex + 1 : null,
            TotalInstallments: rule?.TotalInstallments);
    }

    private static Guid BuildOccurrenceId(Guid seriesId, DateOnly date)
    {
        Span<byte> buffer = stackalloc byte[20];
        seriesId.TryWriteBytes(buffer);
        BitConverter.TryWriteBytes(buffer[16..], date.DayNumber);
        Span<byte> hash = stackalloc byte[16];
        System.Security.Cryptography.MD5.HashData(buffer, hash);
        return new Guid(hash);
    }
}
