namespace Application.DTOs.Invoice.Statistics;

public sealed record InvoiceCategoryProgress(
    Guid CategoryId,
    string CategoryName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal CategoryBottom,
    decimal CategoryTop,
    decimal InvoicedAmount,
    decimal ProjectedAmount,
    decimal InvoicedPercentage,
    decimal ProjectedPercentage);
