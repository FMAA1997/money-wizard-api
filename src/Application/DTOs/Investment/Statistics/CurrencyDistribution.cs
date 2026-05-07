namespace Application.DTOs.Investment.Statistics;

public sealed record CurrencyDistribution(
    IReadOnlyList<CurrencyDistributionEntry> Data,
    int UnvaluedCount);

public sealed record CurrencyDistributionEntry(
    string Currency,
    IReadOnlyDictionary<string, decimal> Value,
    IReadOnlyDictionary<string, decimal> Percentage);
