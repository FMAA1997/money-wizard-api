using Domain.Models;

namespace Application.DTOs.Investment;

public sealed record InvestmentDetailResponse(
    Guid Id,
    Guid UserId,
    AssetClass AssetClass,
    string? Ticker,
    string Description,
    decimal Quantity,
    DateOnly Date,
    decimal? ManualYield,
    decimal? CurrentPrice,
    string? CurrentCurrency,
    DateOnly? PriceAsOf,
    ValuationStatus ValuationStatus,
    IReadOnlyDictionary<string, decimal>? CurrentValue);

public enum ValuationStatus
{
    Unknown,
    Live
}
