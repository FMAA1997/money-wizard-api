using Application.Abstractions.Pricing;
using Domain.Abstractions.Clients;
using Domain.Models;
using ErrorOr;

namespace Infrastructure.Pricing.Providers;

internal abstract class Data912ProviderBase : IPriceProvider
{
    public abstract AssetClass AssetClass { get; }

    protected abstract Task<IReadOnlyDictionary<string, Data912Quote>?> GetSnapshot(CancellationToken cancellationToken);

    protected abstract string DefaultCurrency { get; }

    // Tickers shown when the user hasn't typed anything yet. Order matters — results preserve this order.
    // Tickers not present in the live snapshot are silently skipped so a bad hardcode never breaks the search.
    protected virtual IReadOnlyList<string> FeaturedTickers => Array.Empty<string>();

    protected virtual string ResolveCurrency(string ticker)
    {
        if (string.IsNullOrEmpty(ticker)) return DefaultCurrency;
        var last = char.ToUpperInvariant(ticker[^1]);
        if (last == 'D' || last == 'C') return "USD";
        return DefaultCurrency;
    }

    public async Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default)
    {
        var symbol = Normalize(ticker);
        if (string.IsNullOrEmpty(symbol))
            return Error.Validation("Pricing.TickerRequired", "Ticker is required.");

        var snapshot = await GetSnapshot(cancellationToken);
        if (snapshot is null)
            return Error.Failure("Pricing.Upstream", "Price data unavailable.");

        if (!snapshot.TryGetValue(symbol, out var quote))
            return Error.NotFound("Pricing.TickerNotFound", $"Ticker '{symbol}' not found.");

        return new AssetPrice(symbol, quote.Price, ResolveCurrency(symbol), DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshot(cancellationToken);
        if (snapshot is null)
            return Array.Empty<AssetSearchResult>();

        var q = query?.Trim() ?? string.Empty;

        if (q.Length == 0)
        {
            var featured = new List<AssetSearchResult>(FeaturedTickers.Count);
            foreach (var ticker in FeaturedTickers)
            {
                if (snapshot.ContainsKey(ticker))
                    featured.Add(new AssetSearchResult(ticker, ticker, ResolveCurrency(ticker)));
            }
            return featured;
        }

        return snapshot
            .Where(kvp => kvp.Key.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(kvp => new AssetSearchResult(kvp.Key, kvp.Key, ResolveCurrency(kvp.Key)))
            .ToList();
    }

    protected static string Normalize(string ticker) =>
        string.IsNullOrWhiteSpace(ticker) ? string.Empty : ticker.Trim().ToUpperInvariant();
}
