using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class Data912Client(HttpClient httpClient) : IData912Client
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<Data912Quote>> GetArgStocks(CancellationToken cancellationToken = default) =>
        FetchQuotes("live/arg_stocks", cancellationToken);

    public Task<IReadOnlyList<Data912Quote>> GetArgCedears(CancellationToken cancellationToken = default) =>
        FetchQuotes("live/arg_cedears", cancellationToken);

    public Task<IReadOnlyList<Data912Quote>> GetArgBonds(CancellationToken cancellationToken = default) =>
        FetchQuotes("live/arg_bonds", cancellationToken);

    private async Task<IReadOnlyList<Data912Quote>> FetchQuotes(string path, CancellationToken cancellationToken)
    {
        var payload = await httpClient.GetFromJsonAsync<List<RawQuote>>(path, SerializerOptions, cancellationToken);
        if (payload is null) return [];

        var result = new List<Data912Quote>(payload.Count);
        foreach (var raw in payload)
        {
            if (string.IsNullOrWhiteSpace(raw.Symbol) || raw.C <= 0)
                continue;
            result.Add(new Data912Quote(raw.Symbol.Trim().ToUpperInvariant(), raw.C, raw.PctChange));
        }
        return result;
    }

    private sealed record RawQuote(
        string Symbol,
        decimal C,
        decimal? PctChange);
}
