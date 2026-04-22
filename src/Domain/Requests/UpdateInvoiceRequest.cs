using Domain.Models;

namespace Domain.Requests;

public sealed record UpdateInvoiceRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? Source,
    CreateRecurrenceRequest? Recurrence);
