using Domain.Models;

namespace Domain.Services;

public sealed record PaycheckOccurrence(
    DateOnly Date,
    DateOnly OriginalDate,
    PaycheckSegment Segment,
    PaycheckException? Exception,
    int OccurrenceIndex);

public static class PaycheckSeriesExpander
{
    public static IReadOnlyList<PaycheckOccurrence> Expand(
        PaycheckSeries series,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        var exceptions = series.Exceptions.ToDictionary(e => e.OriginalDate);
        var results = new List<PaycheckOccurrence>();

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
                    AddOccurrence(results, segment.EffectiveFrom, segment, exceptions, occurrenceIndex: 0);
            }
            else
            {
                foreach (var (date, idx) in RecurrenceExpander.Expand(
                    segment.EffectiveFrom, segment.RecurrenceRule, windowStart, windowEnd))
                {
                    AddOccurrence(results, date, segment, exceptions, idx);
                }
            }
        }

        return results;
    }

    public static bool IsValidOccurrence(PaycheckSeries series, DateOnly date)
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

    public static PaycheckSegment? GetSegmentForDate(PaycheckSeries series, DateOnly date)
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

    public static PaycheckSegment? GetActiveSegment(PaycheckSeries series, DateOnly asOfDate)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        if (segments.Count == 0)
            return null;

        var containing = GetSegmentForDate(series, asOfDate);
        if (containing is not null)
            return containing;

        return asOfDate < segments[0].EffectiveFrom ? segments[0] : segments[^1];
    }

    private static DateOnly SegmentEnd(IReadOnlyList<PaycheckSegment> segments, int index)
    {
        var ruleEnd = segments[index].RecurrenceRule?.EndDate ?? DateOnly.MaxValue;
        var nextBoundary = index + 1 < segments.Count
            ? segments[index + 1].EffectiveFrom.AddDays(-1)
            : DateOnly.MaxValue;
        return nextBoundary < ruleEnd ? nextBoundary : ruleEnd;
    }

    private static void AddOccurrence(
        List<PaycheckOccurrence> results,
        DateOnly originalDate,
        PaycheckSegment segment,
        Dictionary<DateOnly, PaycheckException> exceptions,
        int occurrenceIndex)
    {
        if (exceptions.TryGetValue(originalDate, out var exception))
        {
            if (exception.IsDeleted)
                return;

            results.Add(new PaycheckOccurrence(
                Date: exception.Date ?? originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: exception,
                OccurrenceIndex: occurrenceIndex));
        }
        else
        {
            results.Add(new PaycheckOccurrence(
                Date: originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: null,
                OccurrenceIndex: occurrenceIndex));
        }
    }
}
