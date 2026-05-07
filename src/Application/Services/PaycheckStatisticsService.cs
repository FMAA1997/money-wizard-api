using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Paycheck.Statistics;
using Application.Services.Currency;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class PaycheckStatisticsService(
    IPaycheckRepository paycheckRepository,
    ICurrentUserProvider currentUserProvider,
    ICurrencyConverter currencyConverter) : IPaycheckStatisticsService
{
    public async Task<ErrorOr<PaycheckTotals>> GetTotals(int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearPaychecks(year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (paychecks, previousYearStart, currentYearStart, currentYearEnd) = result.Value;
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(paychecks, currentYearStart, currentYearEnd, scope);
        var totalYtd = SumExpandedAmounts(paychecks, currentYearStart, ytdEnd, scope);
        var totalPreviousYear = SumExpandedAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1), scope);

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
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var currentYearMonthly = GetMonthlyAmounts(paychecks, currentYearStart, currentYearEnd, scope);
        var previousYearMonthly = GetMonthlyAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1), scope);

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

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        return new UpcomingPaycheck
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount,
            Currency = earliestCurrency!,
            Amounts = scope.ConvertToDisplay(earliestAmount, earliestCurrency!, earliestDate.Value)
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

    private static Dictionary<int, decimal> GetMonthlyAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var monthly = new Dictionary<int, decimal>();

        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        foreach (var p in oneOffs.Where(p => p.Date >= startDate && p.Date <= endDate))
        {
            var month = p.Date.Month;
            monthly[month] = monthly.GetValueOrDefault(month) + scope.ConvertToPrimary(p.Amount, p.Currency, p.Date);
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amountInPrimary;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amountInPrimary = scope.ConvertToPrimary(exception.Amount, exception.Currency, exception.Date);
                }
                else
                {
                    amountInPrimary = scope.ConvertToPrimary(s.Amount, s.Currency, date);
                }

                var month = date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + amountInPrimary;
            }
        }

        return monthly;
    }

    private static decimal SumExpandedAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var (oneOffs, series, exceptionLookup) = ClassifyPaychecks(paychecks);

        var total = oneOffs
            .Where(p => p.Date >= startDate && p.Date <= endDate)
            .Sum(p => scope.ConvertToPrimary(p.Amount, p.Currency, p.Date));

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (!exception.IsDeleted)
                        total += scope.ConvertToPrimary(exception.Amount, exception.Currency, exception.Date);
                }
                else
                {
                    total += scope.ConvertToPrimary(s.Amount, s.Currency, date);
                }
            }
        }

        return total;
    }
}
