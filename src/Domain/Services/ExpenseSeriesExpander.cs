using Domain.Models;

namespace Domain.Services;

public sealed record ExpenseOccurrence(
    DateOnly Date,
    DateOnly? OriginalDate,
    ExpenseSegment? Segment,
    ExpenseException? Exception,
    int OccurrenceIndex);

public static class ExpenseSeriesExpander
{
    public static IReadOnlyList<ExpenseOccurrence> Expand(
        ExpenseSeries series,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        var overrides = series.Exceptions
            .Where(e => e.OriginalDate.HasValue)
            .ToDictionary(e => e.OriginalDate!.Value);
        var results = new List<ExpenseOccurrence>();

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var segmentEnd = SegmentEnd(segments, i);

            var windowStart = segment.EffectiveFrom > rangeStart ? segment.EffectiveFrom : rangeStart;
            var windowEnd = rangeEnd < segmentEnd ? rangeEnd : segmentEnd;
            if (windowEnd < windowStart)
                continue;

            if (segment.RecurrenceRule is null)
            {
                if (segment.EffectiveFrom >= windowStart && segment.EffectiveFrom <= windowEnd)
                    AddOverrideOrGenerated(results, segment.EffectiveFrom, segment, overrides, occurrenceIndex: 0);
            }
            else
            {
                foreach (var (date, idx) in RecurrenceExpander.Expand(
                    segment.EffectiveFrom, segment.RecurrenceRule, windowStart, windowEnd))
                {
                    AddOverrideOrGenerated(results, date, segment, overrides, idx);
                }
            }
        }

        foreach (var insertion in series.Exceptions)
        {
            if (insertion.OriginalDate.HasValue) continue;
            if (insertion.Date is not { } date) continue;
            if (date < rangeStart || date > rangeEnd) continue;

            results.Add(new ExpenseOccurrence(
                Date: date,
                OriginalDate: null,
                Segment: null,
                Exception: insertion,
                OccurrenceIndex: -1));
        }

        results.Sort((a, b) => a.Date.CompareTo(b.Date));
        return results;
    }

    public static bool IsRecurrenceOccurrence(ExpenseSeries series, DateOnly date)
    {
        var segment = GetSegmentForDate(series, date);
        if (segment is null)
            return false;

        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        var index = segments.IndexOf(segment);
        var segmentEnd = SegmentEnd(segments, index);
        if (date > segmentEnd)
            return false;

        if (segment.RecurrenceRule is null)
            return segment.EffectiveFrom == date;

        return RecurrenceExpander.IsValidOccurrence(segment.EffectiveFrom, segment.RecurrenceRule, date);
    }

    public static bool IsExistingOccurrence(ExpenseSeries series, DateOnly date) =>
        IsRecurrenceOccurrence(series, date)
        || series.Exceptions.Any(e => !e.OriginalDate.HasValue && e.Date == date);

    public static ExpenseSegment? GetSegmentForDate(ExpenseSeries series, DateOnly date)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var nextBoundary = i + 1 < segments.Count ? segments[i + 1].EffectiveFrom : DateOnly.MaxValue;
            if (date >= segment.EffectiveFrom && date < nextBoundary)
                return segment;
        }
        return null;
    }

    public static ExpenseSegment? GetActiveSegment(ExpenseSeries series, DateOnly asOfDate)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        if (segments.Count == 0)
            return null;

        var containing = GetSegmentForDate(series, asOfDate);
        if (containing is not null)
            return containing;

        return asOfDate < segments[0].EffectiveFrom ? segments[0] : segments[^1];
    }

    private static DateOnly SegmentEnd(IReadOnlyList<ExpenseSegment> segments, int index)
    {
        var ruleEnd = segments[index].RecurrenceRule?.EndDate ?? DateOnly.MaxValue;
        var nextBoundary = index + 1 < segments.Count
            ? segments[index + 1].EffectiveFrom.AddDays(-1)
            : DateOnly.MaxValue;
        return nextBoundary < ruleEnd ? nextBoundary : ruleEnd;
    }

    private static void AddOverrideOrGenerated(
        List<ExpenseOccurrence> results,
        DateOnly originalDate,
        ExpenseSegment segment,
        Dictionary<DateOnly, ExpenseException> overrides,
        int occurrenceIndex)
    {
        if (overrides.TryGetValue(originalDate, out var exception))
        {
            if (exception.IsDeleted)
                return;

            results.Add(new ExpenseOccurrence(
                Date: exception.Date ?? originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: exception,
                OccurrenceIndex: occurrenceIndex));
        }
        else
        {
            results.Add(new ExpenseOccurrence(
                Date: originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: null,
                OccurrenceIndex: occurrenceIndex));
        }
    }
}
