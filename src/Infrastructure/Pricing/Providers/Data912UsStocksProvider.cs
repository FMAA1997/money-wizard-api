using Domain.Abstractions.Clients;
using Domain.Models;

namespace Infrastructure.Pricing.Providers;

internal sealed class Data912UsStocksProvider(Data912SnapshotCache cache) : Data912ProviderBase
{
    public override AssetClass AssetClass => AssetClass.Stock;

    protected override string DefaultCurrency => "USD";

    protected override string ResolveCurrency(string ticker) => "USD";

    protected override Task<IReadOnlyDictionary<string, Data912Quote>?> GetSnapshot(CancellationToken cancellationToken) =>
        cache.GetUsStocks(cancellationToken);
}
