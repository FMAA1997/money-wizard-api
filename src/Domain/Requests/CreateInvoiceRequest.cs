using Domain.Models;

namespace Domain.Requests;

public sealed record CreateInvoiceRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? Source,
    CreateRecurrenceRequest? Recurrence);
