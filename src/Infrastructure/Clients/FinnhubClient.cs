using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class FinnhubClient(HttpClient httpClient) : IFinnhubClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<decimal?> GetQuote(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        var path = $"quote?symbol={Uri.EscapeDataString(symbol)}";
        var payload = await httpClient.GetFromJsonAsync<QuoteResponse>(path, SerializerOptions, cancellationToken);

        // Finnhub returns c=0 when the symbol is unknown rather than 404ing.
        if (payload is null || payload.C <= 0) return null;
        return payload.C;
    }

    public async Task<IReadOnlyList<FinnhubSymbol>> SearchSymbols(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return [];

        var path = $"search?q={Uri.EscapeDataString(q)}&exchange=US";
        var payload = await httpClient.GetFromJsonAsync<SearchResponse>(path, SerializerOptions, cancellationToken);
        if (payload?.Result is null) return [];

        var result = new List<FinnhubSymbol>(payload.Result.Count);
        foreach (var raw in payload.Result)
        {
            if (string.IsNullOrWhiteSpace(raw.Symbol) || string.IsNullOrWhiteSpace(raw.Description))
                continue;
            // Drop non-US listings that leak through (e.g. AAPL.SW). Naked US symbols have no dot.
            if (raw.Symbol.Contains('.')) continue;
            result.Add(new FinnhubSymbol(raw.Symbol, raw.Description, raw.Type ?? string.Empty));
        }
        return result;
    }

    private sealed record QuoteResponse(decimal C);
    private sealed record SearchResponse(List<RawSymbol> Result);
    private sealed record RawSymbol(string Symbol, string Description, string? Type);
}
