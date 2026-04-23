using Domain.Abstractions.Clients;
using Domain.Models;

namespace Infrastructure.Pricing.Providers;

internal sealed class Data912CedearsProvider(Data912SnapshotCache cache) : Data912ProviderBase
{
    public override AssetClass AssetClass => AssetClass.Cedear;

    // CEDEARs trade in ARS by default on BYMA. Tickers ending in 'D' (MEP) or 'C' (CCL) are USD.
    protected override string DefaultCurrency => "ARS";

    protected override IReadOnlyList<string> FeaturedTickers { get; } =
        ["AAPL", "MSFT", "NVDA", "AMZN", "GOOGL", "META", "TSLA"];

    protected override Task<IReadOnlyDictionary<string, Data912Quote>?> GetSnapshot(CancellationToken cancellationToken) =>
        cache.GetArgCedears(cancellationToken);
}
