using Domain.Models;

namespace Application.DTOs.Investment.Statistics;

public sealed record AssetClassDistribution(
    IReadOnlyList<AssetClassDistributionEntry> Data,
    int UnvaluedCount);

public sealed record AssetClassDistributionEntry(
    AssetClass AssetClass,
    IReadOnlyDictionary<string, decimal> Value,
    IReadOnlyDictionary<string, decimal> Percentage);
