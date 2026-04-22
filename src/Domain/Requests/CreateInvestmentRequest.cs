using Domain.Models;

namespace Domain.Requests;

public sealed record CreateInvestmentRequest(
    AssetClass AssetClass,
    string? Ticker,
    string Description,
    decimal Quantity,
    DateOnly Date,
    decimal? ManualYield);
