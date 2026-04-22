namespace Application.Abstractions.Pricing;

public sealed record AssetPrice(string Ticker, decimal Price, string Currency, DateOnly AsOf);
