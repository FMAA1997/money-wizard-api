using Domain.Models;

namespace Domain.Services;

public sealed record InvoiceOccurrence(
    DateOnly Date,
    DateOnly OriginalDate,
    InvoiceSegment Segment,
    InvoiceException? Exception,
    int OccurrenceIndex,
    int GlobalIndex,
    long? Number);

public static class InvoiceSeriesExpander
{
    public static IReadOnlyList<InvoiceOccurrence> Expand(
        InvoiceSeries series,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        var exceptions = series.Exceptions.ToDictionary(e => e.OriginalDate);
        var results = new List<InvoiceOccurrence>();

        var globalIndex = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var segmentEnd = SegmentEnd(segments, i);

            if (segment.RecurrenceRule is null)
            {
                if (segment.EffectiveFrom <= segmentEnd)
                {
                    if (segment.EffectiveFrom >= rangeStart && segment.EffectiveFrom <= rangeEnd)
                        AddOccurrence(results, segment.EffectiveFrom, segment, exceptions, 0, globalIndex, series.BaseNumber);
                    globalIndex++;
                }
            }
            else
            {
                var expansionEnd = rangeEnd < segmentEnd ? rangeEnd : segmentEnd;
                foreach (var (date, idx) in RecurrenceExpander.Expand(
                    segment.EffectiveFrom, segment.RecurrenceRule, segment.EffectiveFrom, expansionEnd))
                {
                    if (date >= rangeStart && date <= rangeEnd)
                        AddOccurrence(results, date, segment, exceptions, idx, globalIndex, series.BaseNumber);
                    globalIndex++;
                }
            }
        }

        return results;
    }

    public static bool IsValidOccurrence(InvoiceSeries series, DateOnly date)
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

    public static InvoiceSegment? GetSegmentForDate(InvoiceSeries series, DateOnly date)
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

    public static InvoiceSegment? GetActiveSegment(InvoiceSeries series, DateOnly asOfDate)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        if (segments.Count == 0)
            return null;

        var containing = GetSegmentForDate(series, asOfDate);
        if (containing is not null)
            return containing;

        return asOfDate < segments[0].EffectiveFrom ? segments[0] : segments[^1];
    }

    /// <summary>
    /// Returns the global occurrence index for a specific date within the series, or -1 if not a valid occurrence.
    /// Used by the parent-materialization flow to freeze a Number on the materialized exception.
    /// </summary>
    public static int GetGlobalOccurrenceIndex(InvoiceSeries series, DateOnly date)
    {
        var segments = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        var globalIndex = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var segmentEnd = SegmentEnd(segments, i);

            if (segment.RecurrenceRule is null)
            {
                if (segment.EffectiveFrom == date)
                    return globalIndex;
                if (segment.EffectiveFrom <= segmentEnd)
                    globalIndex++;
            }
            else
            {
                var expansionEnd = date < segmentEnd ? date : segmentEnd;
                foreach (var (occDate, _) in RecurrenceExpander.Expand(
                    segment.EffectiveFrom, segment.RecurrenceRule, segment.EffectiveFrom, expansionEnd))
                {
                    if (occDate == date)
                        return globalIndex;
                    globalIndex++;
                }
            }
        }
        return -1;
    }

    private static DateOnly SegmentEnd(IReadOnlyList<InvoiceSegment> segments, int index)
    {
        var ruleEnd = segments[index].RecurrenceRule?.EndDate ?? DateOnly.MaxValue;
        var nextBoundary = index + 1 < segments.Count
            ? segments[index + 1].EffectiveFrom.AddDays(-1)
            : DateOnly.MaxValue;
        return nextBoundary < ruleEnd ? nextBoundary : ruleEnd;
    }

    private static void AddOccurrence(
        List<InvoiceOccurrence> results,
        DateOnly originalDate,
        InvoiceSegment segment,
        Dictionary<DateOnly, InvoiceException> exceptions,
        int occurrenceIndex,
        int globalIndex,
        long? baseNumber)
    {
        if (exceptions.TryGetValue(originalDate, out var exception))
        {
            if (exception.IsDeleted)
                return;

            long? number = exception.Number ?? (baseNumber + globalIndex);

            results.Add(new InvoiceOccurrence(
                Date: exception.Date ?? originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: exception,
                OccurrenceIndex: occurrenceIndex,
                GlobalIndex: globalIndex,
                Number: number));
        }
        else
        {
            long? number = baseNumber + globalIndex;

            results.Add(new InvoiceOccurrence(
                Date: originalDate,
                OriginalDate: originalDate,
                Segment: segment,
                Exception: null,
                OccurrenceIndex: occurrenceIndex,
                GlobalIndex: globalIndex,
                Number: number));
        }
    }
}
