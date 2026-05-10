namespace Domain.Requests;

public sealed record UpdatePaycheckFromDateRequest(
    decimal Amount,
    string Currency,
    CreateRecurrenceRequest? Recurrence);
