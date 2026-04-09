using Application.DTOs.Shared;

namespace Application.DTOs.Paycheck;

public sealed record PaycheckCalendarResponse(
    IReadOnlyList<string> Months,
    IReadOnlyList<PaycheckCalendarRow> Rows);

public sealed record PaycheckCalendarRow(
    Guid PaycheckId,
    string Description,
    bool IsRecurring,
    RecurrenceInfo? Recurrence,
    IReadOnlyDictionary<string, IReadOnlyList<PaycheckResponse>> Occurrences);
