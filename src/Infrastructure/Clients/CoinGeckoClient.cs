using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class CoinGeckoClient(HttpClient httpClient) : ICoinGeckoClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<string, decimal>> GetUsdPrices(
        IEnumerable<string> coinIds,
        CancellationToken cancellationToken = default)
    {
        var ids = string.Join(",", coinIds.Where(id => !string.IsNullOrWhiteSpace(id)));
        if (ids.Length == 0) return new Dictionary<string, decimal>();

        var path = $"simple/price?ids={Uri.EscapeDataString(ids)}&vs_currencies=usd";
        var payload = await httpClient.GetFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(
            path, SerializerOptions, cancellationToken);

        if (payload is null) return new Dictionary<string, decimal>();

        var result = new Dictionary<string, decimal>(payload.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (coinId, currencyPrices) in payload)
        {
            if (currencyPrices.TryGetValue("usd", out var price) && price > 0)
                result[coinId] = price;
        }
        return result;
    }

    public async Task<IReadOnlyList<CoinGeckoCoin>> ListCoins(CancellationToken cancellationToken = default)
    {
        var payload = await httpClient.GetFromJsonAsync<List<RawCoin>>("coins/list", SerializerOptions, cancellationToken);
        if (payload is null) return [];

        var result = new List<CoinGeckoCoin>(payload.Count);
        foreach (var raw in payload)
        {
            if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Symbol) || string.IsNullOrWhiteSpace(raw.Name))
                continue;
            result.Add(new CoinGeckoCoin(raw.Id, raw.Symbol, raw.Name));
        }
        return result;
    }

    public async Task<IReadOnlyList<CoinGeckoCoin>> SearchCoins(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return [];

        var path = $"search?query={Uri.EscapeDataString(q)}";
        var payload = await httpClient.GetFromJsonAsync<SearchResponse>(path, SerializerOptions, cancellationToken);
        if (payload?.Coins is null) return [];

        // CoinGecko returns coins ordered by market cap rank (coins without a rank come last) — preserve that order.
        var result = new List<CoinGeckoCoin>(payload.Coins.Count);
        foreach (var raw in payload.Coins)
        {
            if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Symbol) || string.IsNullOrWhiteSpace(raw.Name))
                continue;
            result.Add(new CoinGeckoCoin(raw.Id, raw.Symbol, raw.Name));
        }
        return result;
    }

    public async Task<IReadOnlyList<CoinGeckoCoin>> GetTopCoinsByMarketCap(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return [];

        var path = $"coins/markets?vs_currency=usd&order=market_cap_desc&per_page={limit}&page=1&sparkline=false";
        var payload = await httpClient.GetFromJsonAsync<List<MarketCoin>>(path, SerializerOptions, cancellationToken);
        if (payload is null) return [];

        var result = new List<CoinGeckoCoin>(payload.Count);
        foreach (var raw in payload)
        {
            if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Symbol) || string.IsNullOrWhiteSpace(raw.Name))
                continue;
            result.Add(new CoinGeckoCoin(raw.Id, raw.Symbol, raw.Name));
        }
        return result;
    }

    private sealed record RawCoin(string Id, string Symbol, string Name);
    private sealed record SearchResponse(List<RawCoin> Coins);
    private sealed record MarketCoin(string Id, string Symbol, string Name);
}
