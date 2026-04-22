using Domain.Models;

namespace Application.DTOs.Investment;

public sealed record InvestmentPortfolioResponse(
    IReadOnlyDictionary<string, decimal> TotalCurrentValue,
    IReadOnlyList<PortfolioAssetClassBreakdown> ByAssetClass,
    int UnvaluedCount);

public sealed record PortfolioAssetClassBreakdown(
    AssetClass AssetClass,
    IReadOnlyDictionary<string, decimal> CurrentValue,
    IReadOnlyDictionary<string, decimal> WeightPct);
