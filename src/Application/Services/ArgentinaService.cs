using Application.Abstractions.Services;
using Application.DTOs.Argentina.Dollars;
using Application.DTOs.Argentina.Holidays;
using Application.DTOs.Argentina.Macro;
using Domain.Abstractions.Clients;
using ErrorOr;

namespace Application.Services;

public sealed class ArgentinaService(
    IDolarApiClient dolarApiClient,
    IArgentinaDatosClient argentinaDatosClient) : IArgentinaService
{
    private const int MaxHolidays = 50;

    public async Task<ErrorOr<DollarRatesResponse>> GetDollarsAsync(CancellationToken cancellationToken = default)
    {
        var rates = await dolarApiClient.GetDollarRatesAsync(cancellationToken);

        return new DollarRatesResponse
        {
            Rates = [.. rates
                .Select(r => new DollarRate
                {
                    Casa = r.Casa,
                    Nombre = r.Nombre,
                    Moneda = r.Moneda,
                    Compra = r.Compra,
                    Venta = r.Venta,
                    FechaActualizacion = r.FechaActualizacion,
                    Variacion = r.Variacion,
                })],
            LastUpdated = rates.Max(r => r.FechaActualizacion).DateTime
        };
    }

    public async Task<ErrorOr<MacroSummary>> GetMacroAsync(CancellationToken cancellationToken = default)
    {
        var riesgoTask = argentinaDatosClient.GetRiesgoPaisHistoryAsync(cancellationToken);
        var momTask = argentinaDatosClient.GetInflationHistoryAsync(cancellationToken);
        var yoyTask = argentinaDatosClient.GetInflationYoYHistoryAsync(cancellationToken);

        await Task.WhenAll(riesgoTask, momTask, yoyTask);

        var riesgoSeries = SortAscending(riesgoTask.Result);
        var momSeries = SortAscending(momTask.Result);
        var yoySeries = SortAscending(yoyTask.Result);

        if (riesgoSeries.Count == 0 || momSeries.Count == 0 || yoySeries.Count == 0)
            return Error.Failure("ArgentinaMacro.NoData", "Upstream macro indicators returned no data.");

        return new MacroSummary
        {
            RiesgoPais = BuildRiesgoPais(riesgoSeries),
            Mom = BuildInflationPoint(momSeries),
            Yoy = BuildInflationPoint(yoySeries),
        };
    }

    public async Task<ErrorOr<UpcomingHolidaysResponse>> GetUpcomingHolidaysAsync(int n, CancellationToken cancellationToken = default)
    {
        if (n <= 0 || n > MaxHolidays)
            return Error.Validation("ArgentinaHolidays.InvalidCount", $"'n' must be between 1 and {MaxHolidays}.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var currentYearHolidays = await argentinaDatosClient.GetHolidaysAsync(today.Year, cancellationToken);
        var upcoming = currentYearHolidays
            .Where(h => h.Fecha >= today)
            .OrderBy(h => h.Fecha)
            .ToList();

        if (upcoming.Count < n)
        {
            var nextYearHolidays = await argentinaDatosClient.GetHolidaysAsync(today.Year + 1, cancellationToken);
            upcoming.AddRange(nextYearHolidays.OrderBy(h => h.Fecha));
        }

        return new UpcomingHolidaysResponse
        {
            Holidays = upcoming
                .Take(n)
                .Select(h => new Holiday
                {
                    Fecha = h.Fecha,
                    Tipo = h.Tipo,
                    Nombre = h.Nombre,
                })
                .ToList(),
        };
    }

    private static List<IndicatorPoint> SortAscending(IReadOnlyList<IndicatorPoint> series) =>
        series.OrderBy(p => p.Fecha).ToList();

    private static RiesgoPais BuildRiesgoPais(List<IndicatorPoint> series)
    {
        var latest = series[^1];
        if (series.Count < 2)
        {
            return new RiesgoPais { Valor = latest.Valor, Fecha = latest.Fecha };
        }

        var previous = series[^2];
        var absolute = latest.Valor - previous.Valor;
        decimal? percent = previous.Valor == 0
            ? null
            : Math.Round((latest.Valor - previous.Valor) / previous.Valor * 100m, 2);

        return new RiesgoPais
        {
            Valor = latest.Valor,
            Fecha = latest.Fecha,
            AbsoluteChangeBps = absolute,
            PercentChange = percent,
            PreviousFecha = previous.Fecha,
        };
    }

    private static InflationPoint BuildInflationPoint(List<IndicatorPoint> series)
    {
        var latest = series[^1];
        if (series.Count < 2)
        {
            return new InflationPoint { Latest = latest.Valor, LatestFecha = latest.Fecha };
        }

        var previous = series[^2];
        return new InflationPoint
        {
            Latest = latest.Valor,
            LatestFecha = latest.Fecha,
            Previous = previous.Valor,
            PreviousFecha = previous.Fecha,
        };
    }
}
