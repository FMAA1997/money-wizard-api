using Domain.Models;

namespace Domain.Requests;

public sealed record UpdateInvoiceRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? Source,
    InvoiceType Type,
    Guid? ParentInvoiceId,
    InvoiceClass? Class,
    int? PointOfSale,
    long? Number,
    CreateRecurrenceRequest? Recurrence);
