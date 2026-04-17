using Application.DTOs.Shared;

namespace Application.DTOs.Invoice;

public sealed record InvoiceCalendarRow(
    Guid InvoiceId,
    string Description,
    InvoiceSourceInfo? Source,
    bool IsRecurring,
    RecurrenceInfo? Recurrence,
    IReadOnlyDictionary<string, IReadOnlyList<InvoiceResponse>> Occurrences);

public sealed record InvoiceSourceInfo(
    Guid Id,
    string Description,
    decimal Amount);
