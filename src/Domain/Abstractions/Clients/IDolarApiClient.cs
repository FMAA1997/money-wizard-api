namespace Domain.Abstractions.Clients;

public interface IDolarApiClient
{
    Task<IReadOnlyList<DollarRateEntry>> GetDollarRatesAsync(CancellationToken cancellationToken = default);
}

public sealed record DollarRateEntry(
    string Casa,
    string Nombre,
    string Moneda,
    decimal Compra,
    decimal Venta,
    DateTimeOffset FechaActualizacion,
    decimal? Variacion);
