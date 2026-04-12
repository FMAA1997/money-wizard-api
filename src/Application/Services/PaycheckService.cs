using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Paycheck;
using Application.DTOs.Shared;
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
    IUnitOfWork unitOfWork) : IPaycheckService
{
    public async Task<ErrorOr<IReadOnlyList<Paycheck>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var paychecks = await paycheckRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);
        return paychecks.ToList();
    }

    public async Task<ErrorOr<Paycheck>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        return paycheck;
    }

    public async Task<ErrorOr<Paycheck>> Create(CreatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var paycheck = new Paycheck
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description,
            RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
            {
                Frequency = request.Recurrence.Frequency,
                Interval = request.Recurrence.Interval,
                EndDate = request.Recurrence.EndDate,
                TotalInstallments = request.Recurrence.TotalInstallments
            }
        };

        await paycheckRepository.Add(paycheck, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return paycheck;
    }

    public async Task<ErrorOr<Paycheck>> Update(Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        paycheck.Date = request.Date;
        paycheck.Amount = request.Amount;
        paycheck.Description = request.Description;
        paycheck.RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
        {
            Frequency = request.Recurrence.Frequency,
            Interval = request.Recurrence.Interval,
            EndDate = request.Recurrence.EndDate,
            TotalInstallments = request.Recurrence.TotalInstallments
        };

        paycheckRepository.Update(paycheck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return paycheck;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        paycheckRepository.Delete(paycheck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<PaycheckResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await paycheckRepository.GetException(id, date, cancellationToken);
        var occurrenceIndex = RecurrenceExpander.GetOccurrenceIndex(series.Date, series.RecurrenceRule, date);

        if (existing is not null)
        {
            existing.Date = request.Date ?? date;
            existing.Amount = request.Amount ?? series.Amount;
            existing.Description = request.Description ?? series.Description;
            existing.IsDeleted = false;

            paycheckRepository.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapOverride(existing, series, occurrenceIndex);
        }

        var exception = new Paycheck
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date ?? date,
            Amount = request.Amount ?? series.Amount,
            Description = request.Description ?? series.Description,
            RecurringPaycheckId = id,
            OriginalDate = date
        };

        await paycheckRepository.Add(exception, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapOverride(exception, series, occurrenceIndex);
    }

    public async Task<ErrorOr<Paycheck>> UpdateFromDate(Guid id, DateOnly date, UpdatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        if (request.Recurrence is not null && request.Recurrence.Interval < 1)
            return RecurrenceErrors.InvalidInterval;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);
        series.RecurrenceRule.EndDate = previousDate;

        if (previousDate is null)
            paycheckRepository.Delete(series);
        else
            paycheckRepository.Update(series);

        await paycheckRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        var recurrence = request.Recurrence;
        var newSeries = new Paycheck
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description,
            RecurrenceRule = recurrence is null ? null : new RecurrenceRule
            {
                Frequency = recurrence.Frequency,
                Interval = recurrence.Interval,
                EndDate = recurrence.EndDate,
                TotalInstallments = recurrence.TotalInstallments
            }
        };

        await paycheckRepository.Add(newSeries, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return newSeries;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await paycheckRepository.GetException(id, date, cancellationToken);

        if (existing is not null)
        {
            existing.IsDeleted = true;
            paycheckRepository.Update(existing);
        }
        else
        {
            var exception = new Paycheck
            {
                UserId = currentUserProvider.UserId,
                Date = date,
                Amount = series.Amount,
                Description = series.Description,
                RecurringPaycheckId = id,
                OriginalDate = date,
                IsDeleted = true
            };
            await paycheckRepository.Add(exception, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await paycheckRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);

        await paycheckRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        if (previousDate is null)
        {
            paycheckRepository.Delete(series);
        }
        else
        {
            series.RecurrenceRule.EndDate = previousDate;
            paycheckRepository.Update(series);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<CalendarResponse<PaycheckCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var paychecks = await paycheckRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);

        var occurrences = ExpandOccurrences(paychecks, startDate, endDate);

        var months = new List<string>();
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);
        while (current <= end)
        {
            months.Add(current.ToString("yyyy-MM"));
            current = current.AddMonths(1);
        }

        var rows = occurrences
            .GroupBy(o => o.RecurringPaycheckId ?? o.Id)
            .Select(g =>
            {
                var first = g.First();
                var monthDict = g
                    .GroupBy(o => o.Date.ToString("yyyy-MM"))
                    .ToDictionary(
                        mg => mg.Key,
                        mg => (IReadOnlyList<PaycheckResponse>)[.. mg.OrderBy(o => o.Date)]);

                return new PaycheckCalendarRow(
                    PaycheckId: g.Key,
                    Description: first.Description,
                    IsRecurring: first.IsRecurring,
                    Recurrence: first.Recurrence,
                    Occurrences: monthDict);
            })
            .ToList();

        var totals = months
            .Select(m => rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])
                .Sum(o => o.Amount))
            .ToList();

        return new CalendarResponse<PaycheckCalendarRow>(months, rows, totals);
    }

    private static List<PaycheckResponse> ExpandOccurrences(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate)
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

        var results = oneOffs
            .Select(MapOneOff)
            .ToList();

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

                    results.Add(MapOverride(exception, s, index));
                }
                else
                {
                    results.Add(MapVirtual(s, date, index));
                }
            }
        }

        results.Sort((a, b) => a.Date.CompareTo(b.Date));
        return results;
    }

    private static PaycheckResponse MapOneOff(Paycheck paycheck) =>
        new(
            Id: paycheck.Id,
            Date: paycheck.Date,
            Amount: paycheck.Amount,
            Description: paycheck.Description,
            IsRecurring: false,
            RecurringPaycheckId: null,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: null,
            InstallmentNumber: null,
            TotalInstallments: null);

    private static PaycheckResponse MapVirtual(Paycheck series, DateOnly date, int occurrenceIndex) =>
        new(
            Id: series.Id,
            Date: date,
            Amount: series.Amount,
            Description: series.Description,
            IsRecurring: true,
            RecurringPaycheckId: series.Id,
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

    private static PaycheckResponse MapOverride(Paycheck exception, Paycheck series, int occurrenceIndex) =>
        new(
            Id: exception.Id,
            Date: exception.Date,
            Amount: exception.Amount,
            Description: exception.Description,
            IsRecurring: true,
            RecurringPaycheckId: exception.RecurringPaycheckId,
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
