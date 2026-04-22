using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class ArgentinaDatosExchangeRateClient : IExchangeRateClient
{
    private const string MepHistoryPath = "v1/cotizaciones/dolares/bolsa";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ArgentinaDatosExchangeRateClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ExchangeRateEntry>> GetMepHistoryAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _httpClient.GetFromJsonAsync<List<ArgentinaDatosQuote>>(
            MepHistoryPath,
            SerializerOptions,
            cancellationToken);

        if (payload is null)
        {
            return [];
        }

        var entries = new List<ExchangeRateEntry>(payload.Count);
        foreach (var quote in payload)
        {
            if (quote.Venta <= 0 || !DateOnly.TryParse(quote.Fecha, out var date))
            {
                continue;
            }

            entries.Add(new ExchangeRateEntry(date, quote.Venta));
        }

        return entries;
    }

    private sealed record ArgentinaDatosQuote(string Fecha, decimal Compra, decimal Venta);
}
