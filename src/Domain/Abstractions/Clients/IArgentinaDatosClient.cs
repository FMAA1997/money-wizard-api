namespace Domain.Abstractions.Clients;

public interface IArgentinaDatosClient
{
    Task<IReadOnlyList<IndicatorPoint>> GetRiesgoPaisHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndicatorPoint>> GetInflationHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndicatorPoint>> GetInflationYoYHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HolidayEntry>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default);
}

public sealed record IndicatorPoint(DateOnly Fecha, decimal Valor);

public sealed record HolidayEntry(DateOnly Fecha, string Tipo, string Nombre);
