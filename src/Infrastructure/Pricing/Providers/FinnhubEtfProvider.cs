using Domain.Abstractions.Clients;
using Domain.Models;

namespace Infrastructure.Pricing.Providers;

internal sealed class FinnhubEtfProvider(IFinnhubClient client) : FinnhubProviderBase(client)
{
    public override AssetClass AssetClass => AssetClass.Etf;

    // Finnhub tags ETFs as "ETP" (Exchange Traded Product) in its symbol metadata.
    protected override ISet<string> AcceptedTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ETP", "ETF" };

    protected override IReadOnlyList<FeaturedAsset> Featured { get; } =
    [
        new("SPY", "SPDR S&P 500 ETF Trust"),
        new("QQQ", "Invesco QQQ Trust"),
        new("VOO", "Vanguard S&P 500 ETF"),
        new("VTI", "Vanguard Total Stock Market ETF"),
        new("IWM", "iShares Russell 2000 ETF"),
        new("DIA", "SPDR Dow Jones Industrial Average ETF"),
        new("EFA", "iShares MSCI EAFE ETF")
    ];
}
