namespace Domain.Requests;

public sealed record UpdateInvoiceOccurrenceRequest(
    DateOnly? Date,
    decimal? Amount,
    string? Currency);
