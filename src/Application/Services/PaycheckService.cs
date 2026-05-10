using Application.Abstractions;
using Application.Abstractions.Services;
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

public sealed class PaycheckService(
    IPaycheckRepository paycheckRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork,
    ICurrencyConverter currencyConverter) : IPaycheckService
{
    public async Task<ErrorOr<IReadOnlyList<PaycheckDetailResponse>>> GetAllInRange(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await paycheckRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);

        return seriesList.Select(MapDetail).ToList();
    }

    public async Task<ErrorOr<PaycheckDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        return MapDetail(series);
    }

    public async Task<ErrorOr<PaycheckSeries>> Create(CreatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var series = new PaycheckSeries
        {
            UserId = currentUserProvider.UserId,
            Description = request.Description,
            Segments =
            {
                new PaycheckSegment
                {
                    EffectiveFrom = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RecurrenceRule = MapRecurrence(request.Recurrence)
                }
            }
        };

        await paycheckRepository.Add(series, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<PaycheckSeries>> Update(Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        series.Description = request.Description;

        paycheckRepository.Update(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        paycheckRepository.Delete(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<PaycheckResponse>> UpdateOccurrence(
        Guid id, DateOnly date, UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        if (!PaycheckSeriesExpander.IsValidOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var segment = PaycheckSeriesExpander.GetSegmentForDate(series, date)!;
        var occurrenceIndex = segment.RecurrenceRule is null
            ? 0
            : RecurrenceExpander.GetOccurrenceIndex(segment.EffectiveFrom, segment.RecurrenceRule, date);

        var existing = await paycheckRepository.GetException(series.Id, date, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        PaycheckException exception;
        if (existing is not null)
        {
            existing.Date = request.Date;
            existing.Amount = request.Amount;
            existing.Currency = request.Currency;
            existing.IsDeleted = false;

            paycheckRepository.UpdateException(existing);
            exception = existing;
        }
        else
        {
            exception = new PaycheckException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                Date = request.Date,
                Amount = request.Amount,
                Currency = request.Currency,
                IsDeleted = false
            };
            await paycheckRepository.AddException(exception, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var occurrence = new PaycheckOccurrence(
            Date: exception.Date ?? date,
            OriginalDate: date,
            Segment: segment,
            Exception: exception,
            OccurrenceIndex: occurrenceIndex);

        return MapOccurrence(occurrence, series, scope);
    }

    public async Task<ErrorOr<PaycheckSeries>> UpdateFromDate(
        Guid id, DateOnly date, UpdatePaycheckFromDateRequest request, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        if (!PaycheckSeriesExpander.IsValidOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var segment = PaycheckSeriesExpander.GetSegmentForDate(series, date)!;

        unitOfWork.BeginTransaction();
        CapOrDeleteSegment(segment, date);

        await paycheckRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
        await paycheckRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);

        var newSegment = new PaycheckSegment
        {
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = request.Amount,
            Currency = request.Currency,
            RecurrenceRule = MapRecurrence(request.Recurrence)
        };

        await paycheckRepository.AddSegment(newSegment, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return (await paycheckRepository.GetById(series.Id, cancellationToken))!;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        if (!PaycheckSeriesExpander.IsValidOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await paycheckRepository.GetException(series.Id, date, cancellationToken);

        if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.Date = null;
            existing.Amount = null;
            existing.Currency = null;
            paycheckRepository.UpdateException(existing);
        }
        else
        {
            var exception = new PaycheckException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                IsDeleted = true
            };
            await paycheckRepository.AddException(exception, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        if (!PaycheckSeriesExpander.IsValidOccurrence(series, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var segment = PaycheckSeriesExpander.GetSegmentForDate(series, date)!;
        CapOrDeleteSegment(segment, date);

        await paycheckRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
        await paycheckRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await paycheckRepository.GetById(series.Id, cancellationToken);
        if (refreshed is not null && refreshed.Segments.Count == 0)
        {
            paycheckRepository.Delete(refreshed);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Deleted;
    }

    public async Task<ErrorOr<CalendarResponse<PaycheckCalendarRow>>> GetCalendar(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await paycheckRepository.GetByUserIdInRange(
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

        var rows = new List<PaycheckCalendarRow>();
        foreach (var series in seriesList)
        {
            var occurrences = PaycheckSeriesExpander.Expand(series, startDate, endDate);
            if (occurrences.Count == 0) continue;

            var responses = occurrences
                .Select(o => MapOccurrence(o, series, scope))
                .OrderBy(r => r.Date)
                .ToList();

            var monthDict = responses
                .GroupBy(r => r.Date.ToString("yyyy-MM"))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<PaycheckResponse>)[.. g.OrderBy(o => o.Date)]);

            var activeSegment = PaycheckSeriesExpander.GetActiveSegment(series, today);
            var isRecurring = series.Segments.Any(s => s.RecurrenceRule is not null);
            var recurrence = activeSegment?.RecurrenceRule is null
                ? null
                : new RecurrenceInfo(
                    activeSegment.EffectiveFrom,
                    activeSegment.RecurrenceRule.Frequency,
                    activeSegment.RecurrenceRule.Interval,
                    activeSegment.RecurrenceRule.EndDate,
                    activeSegment.RecurrenceRule.TotalInstallments);

            rows.Add(new PaycheckCalendarRow(
                PaycheckId: series.Id,
                Description: series.Description,
                IsRecurring: isRecurring,
                Recurrence: recurrence,
                Occurrences: monthDict));
        }

        var totals = months
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<PaycheckCalendarRow>(months, rows, totals);
    }

    private void CapOrDeleteSegment(PaycheckSegment segment, DateOnly boundary)
    {
        if (segment.RecurrenceRule is null)
        {
            paycheckRepository.DeleteSegment(segment);
            return;
        }

        var previous = RecurrenceExpander.GetPreviousOccurrence(segment.EffectiveFrom, segment.RecurrenceRule, boundary);
        if (previous is null)
        {
            paycheckRepository.DeleteSegment(segment);
        }
        else
        {
            segment.RecurrenceRule.EndDate = previous;
            paycheckRepository.UpdateSegment(segment);
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

    private static IReadOnlyDictionary<string, decimal> SumByCurrency(IEnumerable<PaycheckResponse> occurrences)
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

    private static PaycheckDetailResponse MapDetail(PaycheckSeries series) =>
        new(
            Id: series.Id,
            UserId: series.UserId,
            Description: series.Description,
            Segments: series.Segments
                .OrderBy(s => s.EffectiveFrom)
                .Select(s => new PaycheckSegmentResponse(
                    Id: s.Id,
                    EffectiveFrom: s.EffectiveFrom,
                    Amount: s.Amount,
                    Currency: s.Currency,
                    RecurrenceRule: s.RecurrenceRule))
                .ToList(),
            Exceptions: series.Exceptions
                .OrderBy(e => e.OriginalDate)
                .Select(e => new PaycheckExceptionResponse(
                    Id: e.Id,
                    OriginalDate: e.OriginalDate,
                    Date: e.Date,
                    Amount: e.Amount,
                    Currency: e.Currency,
                    IsDeleted: e.IsDeleted))
                .ToList());

    private static PaycheckResponse MapOccurrence(PaycheckOccurrence occurrence, PaycheckSeries series, CurrencyScope scope)
    {
        var segment = occurrence.Segment;
        var amount = occurrence.Exception?.Amount ?? segment.Amount;
        var currency = occurrence.Exception?.Currency ?? segment.Currency;
        var date = occurrence.Date;
        var hasInstallments = segment.RecurrenceRule?.TotalInstallments is not null;

        return new PaycheckResponse(
            Id: occurrence.Exception?.Id ?? series.Id,
            Date: date,
            Amount: amount,
            Currency: currency,
            Amounts: scope.ConvertToDisplay(amount, currency, date),
            Description: series.Description,
            IsRecurring: segment.RecurrenceRule is not null,
            RecurringPaycheckId: segment.RecurrenceRule is not null ? series.Id : null,
            OriginalDate: occurrence.Exception is null ? null : occurrence.OriginalDate,
            IsOverride: occurrence.Exception is not null,
            Recurrence: segment.RecurrenceRule is null
                ? null
                : new RecurrenceInfo(
                    segment.EffectiveFrom,
                    segment.RecurrenceRule.Frequency,
                    segment.RecurrenceRule.Interval,
                    segment.RecurrenceRule.EndDate,
                    segment.RecurrenceRule.TotalInstallments),
            InstallmentNumber: hasInstallments ? occurrence.OccurrenceIndex + 1 : null,
            TotalInstallments: segment.RecurrenceRule?.TotalInstallments);
    }
}
