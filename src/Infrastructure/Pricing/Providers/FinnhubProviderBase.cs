using Application.Abstractions.Pricing;
using Domain.Abstractions.Clients;
using Domain.Models;
using ErrorOr;

namespace Infrastructure.Pricing.Providers;

internal abstract class FinnhubProviderBase(IFinnhubClient client) : IPriceProvider
{
    public abstract AssetClass AssetClass { get; }

    // Finnhub /search type values this provider accepts (case-insensitive). e.g. "Common Stock", "ETP".
    protected abstract ISet<string> AcceptedTypes { get; }

    // Tickers shown when the user hasn't typed anything yet. Order preserved. Descriptions used for the label.
    protected abstract IReadOnlyList<FeaturedAsset> Featured { get; }

    public async Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default)
    {
        var symbol = (ticker ?? string.Empty).Trim().ToUpperInvariant();
        if (symbol.Length == 0)
            return Error.Validation("Pricing.TickerRequired", "Ticker is required.");

        var price = await client.GetQuote(symbol, cancellationToken);
        if (price is null)
            return Error.NotFound("Pricing.TickerNotFound", $"Ticker '{symbol}' not found.");

        return new AssetPrice(symbol, price.Value, "USD", DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();

        if (q.Length == 0)
        {
            return Featured
                .Select(f => new AssetSearchResult(f.Ticker, f.Description, "USD"))
                .ToList();
        }

        var matches = await client.SearchSymbols(q, cancellationToken);
        return matches
            .Where(m => AcceptedTypes.Contains(m.Type))
            .Take(25)
            .Select(m => new AssetSearchResult(m.Symbol.ToUpperInvariant(), m.Description, "USD"))
            .ToList();
    }

    protected sealed record FeaturedAsset(string Ticker, string Description);
}
