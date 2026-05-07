using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class ArgentinaDatosClient : IArgentinaDatosClient
{
    private const string RiesgoPaisPath = "v1/finanzas/indices/riesgo-pais";
    private const string InflationPath = "v1/finanzas/indices/inflacion";
    private const string InflationYoYPath = "v1/finanzas/indices/inflacionInteranual";
    private const string HolidaysPath = "v1/feriados/{0}";

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

    private sealed record ArgentinaDatosIndicator(string Fecha, decimal Valor);

    private sealed record ArgentinaDatosHoliday(string Fecha, string? Tipo, string? Nombre);
}
