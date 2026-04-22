namespace Application.Abstractions.Pricing;

public sealed record HistoricalPricePoint(DateOnly Date, decimal Price);
