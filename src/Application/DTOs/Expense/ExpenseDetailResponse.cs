using Application.DTOs.Invoice;
using Application.DTOs.Paycheck;
using Domain.Models;

namespace Application.DTOs.Expense;

public sealed record ExpenseDetailResponse(
    Guid Id,
    Guid UserId,
    string Description,
    Guid? CategoryId,
    ExpenseCategory? Category,
    IReadOnlyList<ExpenseSegmentResponse> Segments,
    IReadOnlyList<ExpenseExceptionResponse> Exceptions);

public sealed record ExpenseSegmentResponse(
    Guid Id,
    DateOnly EffectiveFrom,
    decimal Amount,
    string Currency,
    RecurrenceRule? RecurrenceRule,
    Guid? PaycheckSeriesId,
    PaycheckSummary? PaycheckSeries,
    Guid? InvoiceSeriesId,
    InvoiceSummary? InvoiceSeries);

public sealed record ExpenseExceptionResponse(
    Guid Id,
    DateOnly? OriginalDate,
    DateOnly? Date,
    decimal? Amount,
    string? Currency,
    bool IsDeleted);
