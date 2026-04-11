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
    IUserRepository userRepository) : IPaycheckStatisticsService
{
    public async Task<ErrorOr<PaycheckTotals>> GetTotals(string? externalId, int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearPaychecks(externalId, year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (paychecks, previousYearStart, currentYearStart, currentYearEnd) = result.Value;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ytdEnd = today < currentYearEnd ? today : currentYearEnd;

        var totalCurrentYear = SumExpandedAmounts(paychecks, currentYearStart, currentYearEnd);
        var totalYtd = SumExpandedAmounts(paychecks, currentYearStart, ytdEnd);
        var totalPreviousYear = SumExpandedAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1));

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

    public async Task<ErrorOr<PaycheckMonthlyIncomeStats>> GetMonthlyIncomeStats(string? externalId, int year, CancellationToken cancellationToken = default)
    {
        var result = await GetYearPaychecks(externalId, year, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var (paychecks, previousYearStart, currentYearStart, currentYearEnd) = result.Value;

        var currentYearMonthly = GetMonthlyAmounts(paychecks, currentYearStart, currentYearEnd);
        var previousYearMonthly = GetMonthlyAmounts(paychecks, previousYearStart, currentYearStart.AddDays(-1));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

    public async Task<ErrorOr<UpcomingPaycheck>> GetUpcoming(string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeEnd = today.AddYears(1);

        var paychecks = await paycheckRepository.GetByUserIdInRange(user.Id, today, rangeEnd, cancellationToken);

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

        DateOnly? earliestDate = null;
        decimal earliestAmount = 0;
        string? earliestDescription = null;

        foreach (var p in oneOffs.Where(p => p.Date >= today))
        {
            if (earliestDate is null || p.Date < earliestDate)
            {
                earliestDate = p.Date;
                earliestAmount = p.Amount;
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
                    earliestDescription = exception.Description;
                }
            }
            else if (earliestDate is null || date < earliestDate)
            {
                earliestDate = date;
                earliestAmount = s.Amount;
                earliestDescription = s.Description;
            }
        }

        if (earliestDate is null)
            return PaycheckErrors.NotFound;

        return new UpcomingPaycheck
        {
            Date = earliestDate.Value,
            RemainingDays = earliestDate.Value.DayNumber - today.DayNumber,
            Description = earliestDescription!,
            Amount = earliestAmount
        };
    }

    private async Task<ErrorOr<(IReadOnlyList<Paycheck> Paychecks, DateOnly PreviousYearStart, DateOnly CurrentYearStart, DateOnly CurrentYearEnd)>>
        GetYearPaychecks(string? externalId, int year, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var previousYearStart = new DateOnly(year - 1, 1, 1);
        var currentYearStart = new DateOnly(year, 1, 1);
        var currentYearEnd = new DateOnly(year, 12, 31);

        var paychecks = await paycheckRepository.GetByUserIdInRange(user.Id, previousYearStart, currentYearEnd, cancellationToken);

        return (paychecks, previousYearStart, currentYearStart, currentYearEnd);
    }

    private static Dictionary<int, decimal> GetMonthlyAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate)
    {
        var monthly = new Dictionary<int, decimal>();

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

        foreach (var p in oneOffs.Where(p => p.Date >= startDate && p.Date <= endDate))
        {
            var month = p.Date.Month;
            monthly[month] = monthly.GetValueOrDefault(month) + p.Amount;
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                decimal amount;
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (exception.IsDeleted)
                        continue;
                    amount = exception.Amount;
                }
                else
                {
                    amount = s.Amount;
                }

                var month = date.Month;
                monthly[month] = monthly.GetValueOrDefault(month) + amount;
            }
        }

        return monthly;
    }

    private static decimal SumExpandedAmounts(IReadOnlyList<Paycheck> paychecks, DateOnly startDate, DateOnly endDate)
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

        var total = oneOffs
            .Where(p => p.Date >= startDate && p.Date <= endDate)
            .Sum(p => p.Amount);

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (!exception.IsDeleted)
                        total += exception.Amount;
                }
                else
                {
                    total += s.Amount;
                }
            }
        }

        return total;
    }
}
