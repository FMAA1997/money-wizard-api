namespace Domain.Requests;

public sealed record CreateExpenseRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    Guid? CategoryId, Guid? PaycheckSeriesId, Guid? InvoiceSeriesId,
    CreateRecurrenceRequest? Recurrence);
