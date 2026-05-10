using Domain.Models;

namespace Application.DTOs.Paycheck;

public sealed record PaycheckDetailResponse(
    Guid Id,
    Guid UserId,
    string Description,
    IReadOnlyList<PaycheckSegmentResponse> Segments,
    IReadOnlyList<PaycheckExceptionResponse> Exceptions);

public sealed record PaycheckSegmentResponse(
    Guid Id,
    DateOnly EffectiveFrom,
    decimal Amount,
    string Currency,
    RecurrenceRule? RecurrenceRule);

public sealed record PaycheckExceptionResponse(
    Guid Id,
    DateOnly OriginalDate,
    DateOnly? Date,
    decimal? Amount,
    string? Currency,
    bool IsDeleted);
