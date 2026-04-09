namespace Domain.Requests;

public sealed record UpdateExpenseOccurrenceRequest(
    DateOnly? Date,
    decimal? Amount,
    string? Description,
    Guid? CategoryId,
    Guid? Source);
