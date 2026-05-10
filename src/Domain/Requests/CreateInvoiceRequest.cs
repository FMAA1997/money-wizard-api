using Domain.Models;

namespace Domain.Requests;

public sealed record CreateInvoiceRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? Source,
    InvoiceType Type,
    Guid? ParentInvoiceSeriesId,
    DateOnly? ParentOriginalDate,
    InvoiceClass? Class,
    int? PointOfSale,
    long? BaseNumber,
    CreateRecurrenceRequest? Recurrence);
