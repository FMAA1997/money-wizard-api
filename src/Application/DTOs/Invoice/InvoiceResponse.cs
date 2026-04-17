using Application.DTOs.Shared;

namespace Application.DTOs.Invoice;

public sealed record InvoiceResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid? Source,
    bool IsRecurring,
    Guid? RecurringInvoiceId,
    DateOnly? OriginalDate,
    bool IsOverride,
    RecurrenceInfo? Recurrence,
    int? InstallmentNumber,
    int? TotalInstallments);
