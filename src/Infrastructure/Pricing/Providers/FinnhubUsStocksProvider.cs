using Domain.Abstractions.Clients;
using Domain.Models;

namespace Infrastructure.Pricing.Providers;

internal sealed class FinnhubUsStocksProvider(IFinnhubClient client) : FinnhubProviderBase(client)
{
    public override AssetClass AssetClass => AssetClass.Stock;

    protected override ISet<string> AcceptedTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Common Stock" };

    protected override IReadOnlyList<FeaturedAsset> Featured { get; } =
    [
        new("AAPL", "Apple Inc"),
        new("MSFT", "Microsoft Corp"),
        new("NVDA", "NVIDIA Corp"),
        new("AMZN", "Amazon.com Inc"),
        new("GOOGL", "Alphabet Inc"),
        new("META", "Meta Platforms Inc"),
        new("TSLA", "Tesla Inc")
    ];
}
