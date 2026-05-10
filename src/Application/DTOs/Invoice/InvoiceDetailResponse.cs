using Application.DTOs.Paycheck;
using Domain.Models;

namespace Application.DTOs.Invoice;

public sealed record InvoiceDetailResponse(
    Guid Id,
    Guid UserId,
    string Description,
    InvoiceType Type,
    InvoiceClass? Class,
    int? PointOfSale,
    long? BaseNumber,
    Guid? ParentExceptionId,
    InvoiceParentSummary? ParentInvoice,
    IReadOnlyList<InvoiceSegmentResponse> Segments,
    IReadOnlyList<InvoiceExceptionResponse> Exceptions);

public sealed record InvoiceSegmentResponse(
    Guid Id,
    DateOnly EffectiveFrom,
    decimal Amount,
    string Currency,
    RecurrenceRule? RecurrenceRule,
    Guid? Source,
    PaycheckSummary? PaycheckSeries);

public sealed record InvoiceExceptionResponse(
    Guid Id,
    DateOnly? OriginalDate,
    DateOnly? Date,
    decimal? Amount,
    string? Currency,
    long? Number,
    bool IsDeleted);

public sealed record InvoiceParentSummary(
    Guid SeriesId,
    DateOnly? OriginalDate,
    long? Number,
    string Description);
