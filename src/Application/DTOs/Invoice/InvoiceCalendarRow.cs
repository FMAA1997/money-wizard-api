using Application.DTOs.Shared;
using Domain.Models;

namespace Application.DTOs.Invoice;

public sealed record InvoiceCalendarRow(
    Guid InvoiceId,
    string Description,
    InvoiceType Type,
    Guid? ParentExceptionId,
    InvoiceClass? Class,
    int? PointOfSale,
    InvoiceSourceInfo? Source,
    bool IsRecurring,
    RecurrenceInfo? Recurrence,
    IReadOnlyDictionary<string, IReadOnlyList<InvoiceResponse>> Occurrences);

public sealed record InvoiceSourceInfo(
    Guid Id,
    string Description,
    decimal Amount);
