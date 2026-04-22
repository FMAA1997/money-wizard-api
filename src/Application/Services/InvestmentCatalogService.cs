using Application.Abstractions.Pricing;
using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class InvestmentCatalogService(IPriceProviderRegistry priceProviderRegistry) : IInvestmentCatalogService
{
    public async Task<ErrorOr<IReadOnlyList<AssetSearchResultResponse>>> Search(
        AssetClass assetClass,
        string query,
        CancellationToken cancellationToken = default)
    {
        var provider = priceProviderRegistry.Get(assetClass);
        var results = await provider.Search(query ?? string.Empty, cancellationToken);
        if (results.IsError)
            return results.Errors;

        return results.Value
            .Select(r => new AssetSearchResultResponse(assetClass, r.Ticker, r.Description, r.Currency))
            .ToList();
    }
}
