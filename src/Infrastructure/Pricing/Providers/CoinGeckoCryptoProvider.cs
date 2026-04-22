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

    // Symbol (UPPER) → CoinGecko id
    private readonly TimedCache<IReadOnlyDictionary<string, string>> _symbolToId = new(ListTtl);
    private readonly TimedCache<IReadOnlyList<CoinGeckoCoin>> _coinList = new(ListTtl);

    // CoinGecko id → USD price
    private readonly TimedCache<IReadOnlyDictionary<string, decimal>> _prices = new(PriceTtl);

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
        var list = await _coinList.GetAsync(async ct => await client.ListCoins(ct), cancellationToken);
        if (list is null)
        {
            // Seeded fallback — always support BTC/ETH.
            var seeded = new List<AssetSearchResult>
            {
                new("BTC", "Bitcoin", "USD"),
                new("ETH", "Ethereum", "USD")
            };
            return FilterSeeded(seeded, query);
        }

        var q = (query ?? string.Empty).Trim();
        IEnumerable<CoinGeckoCoin> source = list;
        if (q.Length > 0)
        {
            source = source.Where(c =>
                c.Symbol.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return source
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

    private static List<AssetSearchResult> FilterSeeded(List<AssetSearchResult> seeded, string query)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return seeded;
        return seeded
            .Where(s => s.Ticker.Contains(q, StringComparison.OrdinalIgnoreCase) || s.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
