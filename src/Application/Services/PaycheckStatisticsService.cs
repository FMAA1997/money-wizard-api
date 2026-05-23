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
        var (seriesList, previousYearStart, currentYearStart, currentYearEnd) =
            await GetYearSeries(year, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(seriesList, currentYearStart, currentYearEnd, scope);
        var totalYtd = SumExpandedAmounts(seriesList, currentYearStart, ytdEnd, scope);
        var totalPreviousYear = SumExpandedAmounts(seriesList, previousYearStart, currentYearStart.AddDays(-1), scope);

        var variationYoY = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => totalCurrentYear[c] - totalPreviousYear[c]);

        var variationYoyPctg = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => totalPreviousYear[c] != 0
                ? (totalCurrentYear[c] - totalPreviousYear[c]) / totalPreviousYear[c] * 100
                : 0m);

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
        var (seriesList, previousYearStart, currentYearStart, currentYearEnd) =
            await GetYearSeries(year, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var currentYearMonthly = GetMonthlyAmounts(seriesList, currentYearStart, currentYearEnd, scope);
        var previousYearMonthly = GetMonthlyAmounts(seriesList, previousYearStart, currentYearStart.AddDays(-1), scope);

        var avgMonthlyIncome = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => currentYearMonthly.Values.Sum(m => m[c]) / 12);
        var previousYearAvgMonthlyIncome = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => previousYearMonthly.Values.Sum(m => m[c]) / 12);

        var maxMonthlyIncome = new Dictionary<string, decimal>();
        var minMonthlyIncome = new Dictionary<string, decimal>();
        var maxMonthlyIncomeMonth = new Dictionary<string, int>();
        var minMonthlyIncomeMonth = new Dictionary<string, int>();

        foreach (var c in scope.DisplayCurrencies)
        {
            if (currentYearMonthly.Count == 0)
            {
                maxMonthlyIncome[c] = 0m;
                minMonthlyIncome[c] = 0m;
                maxMonthlyIncomeMonth[c] = 0;
                minMonthlyIncomeMonth[c] = 0;
                continue;
            }

            var maxKv = currentYearMonthly.MaxBy(kv => kv.Value[c]);
            var minKv = currentYearMonthly.MinBy(kv => kv.Value[c]);
            maxMonthlyIncome[c] = maxKv.Value[c];
            minMonthlyIncome[c] = minKv.Value[c];
            maxMonthlyIncomeMonth[c] = maxKv.Key;
            minMonthlyIncomeMonth[c] = minKv.Key;
        }

        return new PaycheckMonthlyIncomeStats
        {
            AvgMonthlyIncome = avgMonthlyIncome,
            PreviousYearAvgMonthlyIncome = previousYearAvgMonthlyIncome,
            MaxMonthlyIncome = maxMonthlyIncome,
            MinMonthlyIncome = minMonthlyIncome,
            MaxMonthlyIncomeMonth = maxMonthlyIncomeMonth,
            MinMonthlyIncomeMonth = minMonthlyIncomeMonth
        };
    }

    public async Task<ErrorOr<UpcomingPaycheck>> GetUpcoming(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeEnd = today.AddYears(1);

        var seriesList = await paycheckRepository.GetByUserIdInRange(
            currentUserProvider.UserId, today, rangeEnd, cancellationToken);

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestCurrency = null;
        string? earliestDescription = null;

        foreach (var series in seriesList)
        {
            var occurrences = PaycheckSeriesExpander.Expand(series, today, rangeEnd);
            if (occurrences.Count == 0) continue;

            var first = occurrences[0];
            if (earliestDate is null || first.Date < earliestDate)
            {
                earliestDate = first.Date;
                earliestAmount = first.Exception?.Amount ?? first.Segment!.Amount;
                earliestCurrency = first.Exception?.Currency ?? first.Segment!.Currency;
                earliestDescription = series.Description;
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

    private async Task<(IReadOnlyList<PaycheckSeries> SeriesList, DateOnly PreviousYearStart, DateOnly CurrentYearStart, DateOnly CurrentYearEnd)>
        GetYearSeries(int year, CancellationToken cancellationToken)
    {
        var previousYearStart = new DateOnly(year - 1, 1, 1);
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var seriesList = await paycheckRepository.GetByUserIdInRange(
            currentUserProvider.UserId, previousYearStart, currentYearEnd, cancellationToken);

        return (seriesList, previousYearStart, currentYearStart, currentYearEnd);
    }

    private static Dictionary<int, IReadOnlyDictionary<string, decimal>> GetMonthlyAmounts(
        IReadOnlyList<PaycheckSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var monthly = new Dictionary<int, CurrencyTotals>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                var month = occurrence.Date.Month;
                if (!monthly.TryGetValue(month, out var totals))
                {
                    totals = scope.NewTotals();
                    monthly[month] = totals;
                }
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return monthly.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, decimal>)kv.Value.ToDictionary());
    }

    private static IReadOnlyDictionary<string, decimal> SumExpandedAmounts(
        IReadOnlyList<PaycheckSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                totals.Add(amount, currency, occurrence.Date);
            }
        }
        return totals.ToDictionary();
    }
}
