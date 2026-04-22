using Application.Abstractions.Pricing;
using Domain.Models;

namespace Infrastructure.Pricing;

internal sealed class PriceProviderRegistry : IPriceProviderRegistry
{
    private readonly IReadOnlyDictionary<AssetClass, IPriceProvider> _providers;

    public PriceProviderRegistry(IEnumerable<IPriceProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.AssetClass);
    }

    public IPriceProvider Get(AssetClass assetClass)
    {
        if (_providers.TryGetValue(assetClass, out var provider))
            return provider;

        throw new InvalidOperationException($"No IPriceProvider registered for AssetClass.{assetClass}.");
    }
}
