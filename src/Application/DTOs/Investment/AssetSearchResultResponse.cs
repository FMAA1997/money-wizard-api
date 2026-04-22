using Domain.Models;

namespace Application.DTOs.Investment;

public sealed record AssetSearchResultResponse(
    AssetClass AssetClass,
    string Ticker,
    string Description,
    string Currency);
