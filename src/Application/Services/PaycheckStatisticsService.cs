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
        var (seriesList, previousYearStart, currentYearStart, currentYearEnd) =
            await GetYearSeries(year, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var currentYearMonthly = GetMonthlyAmounts(seriesList, currentYearStart, currentYearEnd, scope);
        var previousYearMonthly = GetMonthlyAmounts(seriesList, previousYearStart, currentYearStart.AddDays(-1), scope);

        var avgMonthlyIncome = currentYearMonthly.Values.Sum() / 12;
        var previousYearAvgMonthlyIncome = previousYearMonthly.Values.Sum() / 12;

        var maxMonth = currentYearMonthly.Count > 0 ? currentYearMonthly.MaxBy(kv => kv.Value) : default;
        var minMonth = currentYearMonthly.Count > 0 ? currentYearMonthly.MinBy(kv => kv.Value) : default;

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

    private static Dictionary<int, decimal> GetMonthlyAmounts(
        IReadOnlyList<PaycheckSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var monthly = new Dictionary<int, decimal>();
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                var month = occurrence.Date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }
        }
        return monthly;
    }

    private static decimal SumExpandedAmounts(
        IReadOnlyList<PaycheckSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        decimal total = 0;
        foreach (var series in seriesList)
        {
            foreach (var occurrence in PaycheckSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                total += scope.ConvertToPrimary(amount, currency, occurrence.Date);
            }
        }
        return total;
    }
}
