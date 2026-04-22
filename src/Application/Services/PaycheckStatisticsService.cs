using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Paycheck.Statistics;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class PaycheckStatisticsService(
    IPaycheckRepository paycheckRepository,
    ICurrentUserProvider currentUserProvider,
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext) : IPaycheckStatisticsService
{
    public async Task<ErrorOr<PaycheckTotals>> GetTotals(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearPaychecks(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (paychecks, previousYearStart, currentYearStart, currentYearEnd) = result.Value;
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(paychecks, currentYearStart, currentYearEnd, lookup, primaryCurrency);
        var totalYtd = SumExpandedAmounts(paychecks, currentYearStart, ytdEnd, lookup, primaryCurrency);
        var totalPreviousYear = SumExpandedAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1), lookup, primaryCurrency);

        var variationYoY = totalCurrentYear - totalPreviousYear;
        var variationYoyPctg = totalPreviousYear != 0
            ? (totalCurrentYear - totalPreviousYear) / totalPreviousYear * 100
            : 0;

        return new PaycheckTotals
        {
            TotalYTD = totalYtd,
            TotalCurrentYear = totalCurrentYear,
            TotalPreviousYear = totalPreviousYear,
            VariationYoY = variationYoY,
            VariationYoyPctg = variationYoyPctg
        };
    }

    public async Task<ErrorOr<PaycheckMonthlyIncomeStats>> GetMonthlyIncomeStats(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearPaychecks(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (paychecks, previousYearStart, currentYearStart, currentYearEnd) = result.Value;
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var currentYearMonthly = GetMonthlyAmounts(paychecks, currentYearStart, currentYearEnd, lookup, primaryCurrency);
        var previousYearMonthly = GetMonthlyAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1), lookup, primaryCurrency);

        var avgMonthlyIncome = currentYearMonthly.Values.Sum() / 12;
        var previousYearAvgMonthlyIncome = previousYearMonthly.Values.Sum() / 12;

        var maxMonth = currentYearMonthly.MaxBy(kv => kv.Value);
        var minMonth = currentYearMonthly.MinBy(kv => kv.Value);

        return new PaycheckMonthlyIncomeStats
        {
            AvgMonthlyIncome = avgMonthlyIncome,
            PreviousYearAvgMonthlyIncome = previousYearAvgMonthlyIncome,
            MaxMonthlyIncome = maxMonth.Value,
            MinMonthlyIncome = minMonth.Value,
            MaxMonthlyIncomeMonth = maxMonth.Key,
            MinMonthlyIncomeMonth = minMonth.Key
        };
    }

    public async Task<ErrorOr<UpcomingPaycheck>> GetUpcoming(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeEnd = today.AddYears(1);

        var paychecks = await paycheckRepository.GetByUserIdInRange(currentUserProvider.UserId, today, rangeEnd, cancellationToken);

        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestCurrency = null;
        string? earliestDescription = null;

        foreach (var p in oneOffs.Where(p => p.Date >= today))
        {
            if (earliestDate is null || p.Date < earliestDate)
            {
                earliestDate = p.Date;
                earliestAmount = p.Amount;
                earliestCurrency = p.Currency;
                earliestDescription = p.Description;
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
            return PaycheckErrors.NotFound;

        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var displayCurrencies = (await userCurrencyContext.ResolveAsync(cancellationToken)).DisplayCurrencies;

        return new UpcomingPaycheck
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount,
            Currency = earliestCurrency!,
            Amounts = lookup.ConvertToAll(earliestAmount, earliestCurrency!, displayCurrencies, earliestDate.Value)
        };
    }

    private async Task<ErrorOr<(IReadOnlyList<Paycheck> Paychecks, DateOnly PreviousYearStart, DateOnly CurrentYearStart, DateOnly CurrentYearEnd)>>
        GetYearPaychecks(int year, CancellationToken cancellationToken)
    {
        var previousYearStart = new DateOnly(year - 1, 1, 1);
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var paychecks = await paycheckRepository.GetByUserIdInRange(currentUserProvider.UserId, previousYearStart, currentYearEnd, cancellationToken);

        return (paychecks, previousYearStart, currentYearStart, currentYearEnd);
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

    private static Dictionary<int, decimal> GetMonthlyAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var monthly = new Dictionary<int, decimal>();

        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        foreach (var p in oneOffs.Where(p => p.Date >= startDate && p.Date <= endDate))
        {
            var month = p.Date.Month;
            monthly[month] = monthly.GetValueOrDefault(month) + lookup.Convert(p.Amount, p.Currency, targetCurrency, p.Date);
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

    private static decimal SumExpandedAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CurrencyLookup lookup, string targetCurrency)
    {
        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        var total = oneOffs
            .Where(p => p.Date >= startDate && p.Date <= endDate)
            .Sum(p => lookup.Convert(p.Amount, p.Currency, targetCurrency, p.Date));

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
