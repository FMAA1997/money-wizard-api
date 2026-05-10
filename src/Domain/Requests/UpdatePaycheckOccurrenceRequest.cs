namespace Domain.Requests;

public sealed record UpdatePaycheckOccurrenceRequest(
    DateOnly? Date,
    decimal? Amount,
    string? Currency);
