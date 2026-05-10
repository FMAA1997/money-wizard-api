namespace Domain.Requests;

public sealed record UpdateExpenseFromDateRequest(
    decimal Amount,
    string Currency,
    Guid? PaycheckSeriesId,
    Guid? InvoiceSeriesId,
    CreateRecurrenceRequest? Recurrence);
