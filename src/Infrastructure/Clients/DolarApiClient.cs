using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class DolarApiClient : IDolarApiClient
{
    private const string DollarsPath = "v1/dolares";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public DolarApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DollarRateEntry>> GetDollarRatesAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _httpClient.GetFromJsonAsync<List<DolarApiQuote>>(
            DollarsPath, SerializerOptions, cancellationToken);

        if (payload is null)
            return [];

        var entries = new List<DollarRateEntry>(payload.Count);
        foreach (var quote in payload)
        {
            if (string.IsNullOrWhiteSpace(quote.Casa) || string.IsNullOrWhiteSpace(quote.Moneda))
                continue;

            entries.Add(new DollarRateEntry(
                Casa: quote.Casa,
                Nombre: quote.Nombre ?? quote.Casa,
                Moneda: quote.Moneda,
                Compra: quote.Compra,
                Venta: quote.Venta,
                FechaActualizacion: quote.FechaActualizacion,
                Variacion: quote.Variacion));
        }

        return entries;
    }

    private sealed record DolarApiQuote(
        string? Moneda,
        string? Casa,
        string? Nombre,
        decimal Compra,
        decimal Venta,
        DateTimeOffset FechaActualizacion,
        decimal? Variacion);
}
