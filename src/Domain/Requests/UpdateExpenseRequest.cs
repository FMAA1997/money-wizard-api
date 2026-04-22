using Domain.Models;

namespace Domain.Requests;

public sealed record UpdateExpenseRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? CategoryId, Guid? PaycheckId, Guid? InvoiceId,
    CreateRecurrenceRequest? Recurrence);
