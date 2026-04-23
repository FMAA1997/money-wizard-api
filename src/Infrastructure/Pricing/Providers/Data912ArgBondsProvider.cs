using Domain.Abstractions.Clients;
using Domain.Models;

namespace Infrastructure.Pricing.Providers;

internal sealed class Data912ArgBondsProvider(Data912SnapshotCache cache) : Data912ProviderBase
{
    public override AssetClass AssetClass => AssetClass.Bond;

    // AR sovereign bonds trade in ARS by default. Tickers ending in 'D' (MEP) or 'C' (CCL) are USD.
    protected override string DefaultCurrency => "ARS";

    protected override IReadOnlyList<string> FeaturedTickers { get; } =
        ["AL30", "GD30", "AL29", "GD29", "AE38", "GD38", "AL35", "GD35"];

    protected override Task<IReadOnlyDictionary<string, Data912Quote>?> GetSnapshot(CancellationToken cancellationToken) =>
        cache.GetArgBonds(cancellationToken);
}
