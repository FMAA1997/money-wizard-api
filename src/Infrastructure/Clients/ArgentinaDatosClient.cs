using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed partial class ArgentinaDatosClient : IArgentinaDatosClient
{
    private const string RiesgoPaisPath = "v1/finanzas/indices/riesgo-pais";
    private const string InflationPath = "v1/finanzas/indices/inflacion";
    private const string InflationYoYPath = "v1/finanzas/indices/inflacionInteranual";
    private const string HolidaysPath = "v1/feriados/{0}";
    private const string FciMercadoDineroPath = "v1/finanzas/fci/mercadoDinero/ultimo";
    private const string FciRentaFijaPath = "v1/finanzas/fci/rentaFija/ultimo";
    private const string FciRentaVariablePath = "v1/finanzas/fci/rentaVariable/ultimo";
    private const string FciRentaMixtaPath = "v1/finanzas/fci/rentaMixta/ultimo";

    private const string FciCategoryMercadoDinero = "Mercado Dinero";
    private const string FciCategoryRentaFija = "Renta Fija";
    private const string FciCategoryRentaVariable = "Renta Variable";
    private const string FciCategoryRentaMixta = "Renta Mixta";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ArgentinaDatosClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<IReadOnlyList<IndicatorPoint>> GetRiesgoPaisHistoryAsync(CancellationToken cancellationToken = default) =>
        GetIndicatorSeriesAsync(RiesgoPaisPath, cancellationToken);

    public Task<IReadOnlyList<IndicatorPoint>> GetInflationHistoryAsync(CancellationToken cancellationToken = default) =>
        GetIndicatorSeriesAsync(InflationPath, cancellationToken);

    public Task<IReadOnlyList<IndicatorPoint>> GetInflationYoYHistoryAsync(CancellationToken cancellationToken = default) =>
        GetIndicatorSeriesAsync(InflationYoYPath, cancellationToken);

    public async Task<IReadOnlyList<HolidayEntry>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default)
    {
        var path = string.Format(HolidaysPath, year);
        var payload = await _httpClient.GetFromJsonAsync<List<ArgentinaDatosHoliday>>(
            path, SerializerOptions, cancellationToken);

        if (payload is null)
            return [];

        var entries = new List<HolidayEntry>(payload.Count);
        foreach (var item in payload)
        {
            if (!DateOnly.TryParse(item.Fecha, out var date) || string.IsNullOrWhiteSpace(item.Nombre))
                continue;

            entries.Add(new HolidayEntry(date, item.Tipo ?? string.Empty, item.Nombre));
        }

        return entries;
    }

    private async Task<IReadOnlyList<IndicatorPoint>> GetIndicatorSeriesAsync(string path, CancellationToken cancellationToken)
    {
        var payload = await _httpClient.GetFromJsonAsync<List<ArgentinaDatosIndicator>>(
            path, SerializerOptions, cancellationToken);

        if (payload is null)
            return [];

        var entries = new List<IndicatorPoint>(payload.Count);
        foreach (var item in payload)
        {
            if (!DateOnly.TryParse(item.Fecha, out var date))
                continue;

            entries.Add(new IndicatorPoint(date, item.Valor));
        }

        return entries;
    }

    public Task<IReadOnlyList<FciQuote>> GetFciMercadoDineroAsync(CancellationToken cancellationToken = default) =>
        FetchFciCategoryAsync(FciMercadoDineroPath, FciCategoryMercadoDinero, cancellationToken);

    public Task<IReadOnlyList<FciQuote>> GetFciRentaFijaAsync(CancellationToken cancellationToken = default) =>
        FetchFciCategoryAsync(FciRentaFijaPath, FciCategoryRentaFija, cancellationToken);

    public Task<IReadOnlyList<FciQuote>> GetFciRentaVariableAsync(CancellationToken cancellationToken = default) =>
        FetchFciCategoryAsync(FciRentaVariablePath, FciCategoryRentaVariable, cancellationToken);

    public Task<IReadOnlyList<FciQuote>> GetFciRentaMixtaAsync(CancellationToken cancellationToken = default) =>
        FetchFciCategoryAsync(FciRentaMixtaPath, FciCategoryRentaMixta, cancellationToken);

    // argentinadatos publishes `vcp` per 1000 cuotapartes (matching CAFCI's convention) —
    // scale to per-unit so it composes with the user's stored Quantity.
    private const decimal FciVcpRatio = 0.001m;

    private async Task<IReadOnlyList<FciQuote>> FetchFciCategoryAsync(string path, string category, CancellationToken cancellationToken)
    {
        var payload = await _httpClient.GetFromJsonAsync<List<ArgentinaDatosFciItem>>(
            path, SerializerOptions, cancellationToken);

        if (payload is null)
            return [];

        var quotes = new List<FciQuote>(payload.Count);
        foreach (var item in payload)
        {
            if (string.IsNullOrWhiteSpace(item.Fondo) || item.Vcp is not > 0)
                continue;
            if (!DateOnly.TryParse(item.Fecha, out var asOf))
                continue;

            var fundName = item.Fondo.Trim();
            var ticker = ComputeFciTicker(fundName);
            var currency = ResolveFciCurrency(fundName);
            quotes.Add(new FciQuote(ticker, fundName, category, item.Vcp.Value * FciVcpRatio, currency, asOf));
        }
        return quotes;
    }

    // SHA1(NORMALIZED_FUND) → first 10 hex chars, prefixed "FCI_" → 14 chars total.
    // Fund identity in argentinadatos = the full `fondo` string (fund + class merged).
    internal static string ComputeFciTicker(string fundName)
    {
        var normalized = NormalizeFundName(fundName);
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(bytes)[..10].ToLowerInvariant();
        return $"FCI_{hex}";
    }

    internal static string ResolveFciCurrency(string fundName)
    {
        // argentinadatos doesn't expose a moneda field; "Dolar"/"Dólar"/"USD" in the fund label is the cue.
        var upper = fundName.ToUpperInvariant();
        if (upper.Contains("DOLAR") || upper.Contains("DÓLAR") || upper.Contains("USD"))
            return "USD";
        return "ARS";
    }

    private static string NormalizeFundName(string s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : WhitespaceRegex().Replace(s.Trim().ToUpperInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record ArgentinaDatosIndicator(string Fecha, decimal Valor);

    private sealed record ArgentinaDatosHoliday(string Fecha, string? Tipo, string? Nombre);

    private sealed record ArgentinaDatosFciItem(string? Fondo, string? Fecha, decimal? Vcp);
}
