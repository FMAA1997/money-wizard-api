using Application.Abstractions.Pricing;
using Domain.Abstractions.Clients;
using Domain.Models;
using ErrorOr;
using Infrastructure.Services;

namespace Infrastructure.Pricing.Providers;

internal sealed class CoinGeckoCryptoProvider(ICoinGeckoClient client) : IPriceProvider
{
    private static readonly TimeSpan ListTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan PriceTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FeaturedTtl = TimeSpan.FromHours(1);

    private const int FeaturedLimit = 10;

    // Symbol (UPPER) → CoinGecko id
    private readonly TimedCache<IReadOnlyDictionary<string, string>> _symbolToId = new(ListTtl);

    // CoinGecko id → USD price
    private readonly TimedCache<IReadOnlyDictionary<string, decimal>> _prices = new(PriceTtl);

    // Top coins by market cap — used for empty-query search results.
    private readonly TimedCache<IReadOnlyList<CoinGeckoCoin>> _featured = new(FeaturedTtl);

    // Seed with BTC/ETH so crypto pricing works without calling /coins/list first (it's huge).
    private static readonly IReadOnlyDictionary<string, string> SeedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum"
    };

    public AssetClass AssetClass => AssetClass.Crypto;

    public async Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default)
    {
        var symbol = (ticker ?? string.Empty).Trim().ToUpperInvariant();
        if (symbol.Length == 0)
            return Error.Validation("Pricing.TickerRequired", "Ticker is required.");

        var coinId = await ResolveCoinId(symbol, cancellationToken);
        if (coinId is null)
            return Error.NotFound("Pricing.TickerNotFound", $"Crypto symbol '{symbol}' not found.");

        var prices = await _prices.GetAsync(async ct => await client.GetUsdPrices(InterestingIds(), ct), cancellationToken);
        if (prices is null || !prices.TryGetValue(coinId, out var price))
        {
            var fresh = await client.GetUsdPrices([coinId], cancellationToken);
            if (!fresh.TryGetValue(coinId, out price))
                return Error.Failure("Pricing.Upstream", "Price data unavailable.");
        }

        return new AssetPrice(symbol, price, "USD", DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();

        // Empty query → top coins by market cap (cached 1h). Falls back to a BTC/ETH seed if the upstream call fails.
        if (q.Length == 0)
        {
            var featured = await _featured.GetAsync(async ct => await client.GetTopCoinsByMarketCap(FeaturedLimit, ct), cancellationToken);
            if (featured is { Count: > 0 })
            {
                return featured
                    .Select(c => new AssetSearchResult(c.Symbol.ToUpperInvariant(), c.Name, "USD"))
                    .ToList();
            }

            return new List<AssetSearchResult>
            {
                new("BTC", "Bitcoin", "USD"),
                new("ETH", "Ethereum", "USD")
            };
        }

        var coins = await client.SearchCoins(q, cancellationToken);
        return coins
            .Take(25)
            .Select(c => new AssetSearchResult(c.Symbol.ToUpperInvariant(), c.Name, "USD"))
            .ToList();
    }

    private async Task<string?> ResolveCoinId(string symbol, CancellationToken cancellationToken)
    {
        if (SeedMap.TryGetValue(symbol, out var seeded))
            return seeded;

        var map = await _symbolToId.GetAsync(async ct =>
        {
            var list = await client.ListCoins(ct);
            if (list.Count == 0) return null;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var coin in list)
            {
                // First occurrence wins (CoinGecko lists coins alphabetically by id — major coins tend to come first).
                dict.TryAdd(coin.Symbol, coin.Id);
            }
            return dict;
        }, cancellationToken);

        return map?.TryGetValue(symbol, out var id) == true ? id : null;
    }

    private IEnumerable<string> InterestingIds() => SeedMap.Values;
}
