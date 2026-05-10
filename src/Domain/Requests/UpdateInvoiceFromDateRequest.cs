namespace Domain.Requests;

public sealed record UpdateInvoiceFromDateRequest(
    decimal Amount,
    string Currency,
    Guid? Source,
    CreateRecurrenceRequest? Recurrence);
