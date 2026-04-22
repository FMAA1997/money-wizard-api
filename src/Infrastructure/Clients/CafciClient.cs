using System.Net.Http.Json;
using System.Text.Json;
using Domain.Abstractions.Clients;

namespace Infrastructure.Clients;

public sealed class CafciClient(HttpClient httpClient) : ICafciClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<decimal?> GetYieldBetween(
        string fundId,
        string classId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fundId) || string.IsNullOrWhiteSpace(classId))
            return null;

        var path = $"fondo/{Uri.EscapeDataString(fundId)}/clase/{Uri.EscapeDataString(classId)}/rendimiento/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}";
        var payload = await httpClient.GetFromJsonAsync<CafciYieldResponse>(path, SerializerOptions, cancellationToken);
        return payload?.Data?.Rendimiento;
    }

    private sealed record CafciYieldResponse(CafciYieldData? Data);
    private sealed record CafciYieldData(decimal? Rendimiento);
}
