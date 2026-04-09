using Domain.Models;

namespace Domain.Services;

public static class RecurrenceExpander
{
    public static IReadOnlyList<(DateOnly Date, int OccurrenceIndex)> Expand(
        DateOnly seriesStart,
        RecurrenceRule rule,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var results = new List<(DateOnly, int)>();
        var effectiveEnd = rangeEnd;

        if (rule.EndDate.HasValue && rule.EndDate.Value < effectiveEnd)
            effectiveEnd = rule.EndDate.Value;

        var index = 0;

        if (rule.TotalInstallments.HasValue)
        {
            var installmentEnd = Advance(seriesStart, rule.Frequency, rule.Interval * (rule.TotalInstallments.Value - 1));
            if (installmentEnd < effectiveEnd)
                effectiveEnd = installmentEnd;
        }

        while (true)
        {
            var current = Advance(seriesStart, rule.Frequency, rule.Interval * index);

            if (current > effectiveEnd)
                break;

            if (current >= rangeStart)
                results.Add((current, index));

            index++;
        }

        return results;
    }

    public static DateOnly Advance(DateOnly start, RecurrenceFrequency frequency, int totalUnits) =>
        frequency switch
        {
            RecurrenceFrequency.Daily => start.AddDays(totalUnits),
            RecurrenceFrequency.Weekly => start.AddDays(7 * totalUnits),
            RecurrenceFrequency.Monthly => start.AddMonths(totalUnits),
            RecurrenceFrequency.Yearly => start.AddYears(totalUnits),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };

    public static bool IsValidOccurrence(DateOnly seriesStart, RecurrenceRule rule, DateOnly candidateDate)
    {
        if (candidateDate < seriesStart)
            return false;

        if (rule.EndDate.HasValue && candidateDate > rule.EndDate.Value)
            return false;

        var index = 0;
        while (true)
        {
            var occurrence = Advance(seriesStart, rule.Frequency, rule.Interval * index);

            if (occurrence == candidateDate)
            {
                if (rule.TotalInstallments.HasValue && index >= rule.TotalInstallments.Value)
                    return false;
                return true;
            }

            if (occurrence > candidateDate)
                return false;

            index++;

            if (rule.TotalInstallments.HasValue && index >= rule.TotalInstallments.Value)
                return false;
        }
    }

    public static DateOnly? GetPreviousOccurrence(DateOnly seriesStart, RecurrenceRule rule, DateOnly beforeDate)
    {
        DateOnly? previous = null;
        var index = 0;
        while (true)
        {
            var occurrence = Advance(seriesStart, rule.Frequency, rule.Interval * index);

            if (occurrence >= beforeDate)
                return previous;

            if (rule.TotalInstallments.HasValue && index >= rule.TotalInstallments.Value)
                return previous;

            previous = occurrence;
            index++;
        }
    }

    public static int GetOccurrenceIndex(DateOnly seriesStart, RecurrenceRule rule, DateOnly date)
    {
        var index = 0;
        while (true)
        {
            var occurrence = Advance(seriesStart, rule.Frequency, rule.Interval * index);
            if (occurrence == date)
                return index;
            if (occurrence > date)
                return -1;
            index++;
        }
    }
}
