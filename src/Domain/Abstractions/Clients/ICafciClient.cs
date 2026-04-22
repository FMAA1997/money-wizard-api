namespace Domain.Abstractions.Clients;

public interface ICafciClient
{
    /// <summary>
    /// Returns the yield percentage of a fund class between two dates (rendimiento).
    /// CAFCI endpoint: /fondo/{fundId}/clase/{classId}/rendimiento/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}
    /// Response shape: { "data": { "rendimiento": <number> } }
    /// </summary>
    Task<decimal?> GetYieldBetween(
        string fundId,
        string classId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
