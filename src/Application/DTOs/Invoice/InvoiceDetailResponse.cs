using Domain.Models;

namespace Application.DTOs.Invoice;

public sealed record InvoiceDetailResponse(
    Guid Id,
    Guid UserId,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    Guid? Source,
    InvoiceType Type,
    Guid? ParentInvoiceId,
    InvoiceClass? Class,
    int? PointOfSale,
    long? Number,
    RecurrenceRule? RecurrenceRule,
    Guid? RecurringInvoiceId,
    DateOnly? OriginalDate,
    bool IsDeleted,
    Domain.Models.Paycheck? Paycheck,
    IReadOnlyDictionary<string, decimal> Amounts);
