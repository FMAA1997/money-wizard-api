namespace Application.DTOs.Invoice.Statistics;

public sealed record InvoiceCategoryProgress(
    Guid CategoryId,
    string CategoryName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyDictionary<string, decimal> CategoryBottom,
    IReadOnlyDictionary<string, decimal> CategoryTop,
    IReadOnlyDictionary<string, decimal> InvoicedAmount,
    IReadOnlyDictionary<string, decimal> ProjectedAmount,
    IReadOnlyDictionary<string, decimal> InvoicedPercentage,
    IReadOnlyDictionary<string, decimal> ProjectedPercentage);
