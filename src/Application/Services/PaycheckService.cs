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
    IUnitOfWork unitOfWork,
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext) : IPaycheckService
{
    public async Task<ErrorOr<IReadOnlyList<PaycheckDetailResponse>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var paychecks = await paycheckRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return paychecks.Select(p => MapEntityDetail(p, lookup, displayCurrencies)).ToList();
    }

    public async Task<ErrorOr<PaycheckDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != currentUserProvider.UserId)
            return PaycheckErrors.NotFound;

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return MapEntityDetail(paycheck, lookup, displayCurrencies);
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
            Currency = request.Currency,
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
        paycheck.Currency = request.Currency;
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

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var currencyProfile = await userCurrencyContext.ResolveAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Date = request.Date ?? date;
            existing.Amount = request.Amount ?? series.Amount;
            existing.Currency = request.Currency ?? series.Currency;
            existing.Description = request.Description ?? series.Description;
            existing.IsDeleted = false;

            paycheckRepository.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapOverride(existing, series, occurrenceIndex, lookup, currencyProfile.DisplayCurrencies);
        }

        var exception = new Paycheck
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date ?? date,
            Amount = request.Amount ?? series.Amount,
            Currency = request.Currency ?? series.Currency,
            Description = request.Description ?? series.Description,
            RecurringPaycheckId = id,
            OriginalDate = date
        };

        await paycheckRepository.Add(exception, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapOverride(exception, series, occurrenceIndex, lookup, currencyProfile.DisplayCurrencies);
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
            Currency = request.Currency,
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
                Currency = series.Currency,
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

        var occurrences = await ExpandOccurrences(paychecks, startDate, endDate, cancellationToken);

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
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<PaycheckCalendarRow>(months, rows, totals);
    }

    private async Task<List<PaycheckResponse>> ExpandOccurrences(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
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

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var currencyProfile = await userCurrencyContext.ResolveAsync(cancellationToken);
        var displayCurrencies = currencyProfile.DisplayCurrencies;
        var results = new List<PaycheckResponse>();

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

    private static PaycheckDetailResponse MapEntityDetail(Paycheck paycheck, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: paycheck.Id,
            UserId: paycheck.UserId,
            Date: paycheck.Date,
            Amount: paycheck.Amount,
            Currency: paycheck.Currency,
            Description: paycheck.Description,
            RecurrenceRule: paycheck.RecurrenceRule,
            RecurringPaycheckId: paycheck.RecurringPaycheckId,
            OriginalDate: paycheck.OriginalDate,
            IsDeleted: paycheck.IsDeleted,
            Amounts: lookup.ConvertToAll(paycheck.Amount, paycheck.Currency, displayCurrencies, paycheck.Date));

    private static PaycheckResponse MapOneOff(Paycheck paycheck, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: paycheck.Id,
            Date: paycheck.Date,
            Amount: paycheck.Amount,
            Currency: paycheck.Currency,
            Amounts: lookup.ConvertToAll(paycheck.Amount, paycheck.Currency, displayCurrencies, paycheck.Date),
            Description: paycheck.Description,
            IsRecurring: false,
            RecurringPaycheckId: null,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: null,
            InstallmentNumber: null,
            TotalInstallments: null);

    private static PaycheckResponse MapVirtual(Paycheck series, DateOnly date, int occurrenceIndex, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: series.Id,
            Date: date,
            Amount: series.Amount,
            Currency: series.Currency,
            Amounts: lookup.ConvertToAll(series.Amount, series.Currency, displayCurrencies, date),
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

    private static PaycheckResponse MapOverride(Paycheck exception, Paycheck series, int occurrenceIndex, CurrencyLookup lookup, IEnumerable<string> displayCurrencies) =>
        new(
            Id: exception.Id,
            Date: exception.Date,
            Amount: exception.Amount,
            Currency: exception.Currency,
            Amounts: lookup.ConvertToAll(exception.Amount, exception.Currency, displayCurrencies, exception.Date),
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
