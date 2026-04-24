using Application.DTOs.Shared;
using Domain.Models;

namespace Application.DTOs.Invoice;

public sealed record InvoiceResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    IReadOnlyDictionary<string, decimal> Amounts,
    string Description,
    Guid? Source,
    InvoiceType Type,
    Guid? ParentInvoiceId,
    InvoiceClass? Class,
    int? PointOfSale,
    long? Number,
    bool IsRecurring,
    Guid? RecurringInvoiceId,
    DateOnly? OriginalDate,
    bool IsOverride,
    RecurrenceInfo? Recurrence,
    int? InstallmentNumber,
    int? TotalInstallments);
