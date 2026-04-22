namespace Domain.Requests;

public sealed record UpdateExpenseOccurrenceRequest(
    DateOnly? Date,
    decimal? Amount,
    string? Currency,
    string? Description,
    Guid? CategoryId,
    Guid? PaycheckId,
    Guid? InvoiceId);
