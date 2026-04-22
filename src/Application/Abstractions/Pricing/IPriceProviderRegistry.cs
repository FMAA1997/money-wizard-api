using Domain.Models;

namespace Application.Abstractions.Pricing;

public interface IPriceProviderRegistry
{
    IPriceProvider Get(AssetClass assetClass);
}
