using Domain.Abstractions.Clients;
using Infrastructure.Services;

namespace Infrastructure.Pricing;

// Singleton snapshot of all argentinadatos FCI rows, merged across the four category endpoints
// (mercadoDinero / rentaFija / rentaVariable / rentaMixta). The `otros` endpoint is excluded —
// it returns bank "cuenta remunerada" tasas, not FCIs.
// 6h TTL — cuotapartes are published EOD/T-1 by most fund managers; a few-hour cache balances
// freshness with not hammering four endpoints per autocomplete keystroke.
public sealed class ArgentinaDatosFciSnapshotCache(IArgentinaDatosClient client)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    private readonly TimedCache<IReadOnlyDictionary<string, FciQuote>> _all = new(Ttl);

    public Task<IReadOnlyDictionary<string, FciQuote>?> GetAll(CancellationToken ct = default) =>
        _all.GetAsync(LoadAll, ct);

    private async Task<IReadOnlyDictionary<string, FciQuote>?> LoadAll(CancellationToken ct)
    {
        var results = await Task.WhenAll(
            client.GetFciMercadoDineroAsync(ct),
            client.GetFciRentaFijaAsync(ct),
            client.GetFciRentaVariableAsync(ct),
            client.GetFciRentaMixtaAsync(ct));

        var dict = new Dictionary<string, FciQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in results)
            foreach (var q in list)
                dict[q.Ticker] = q;

        return dict.Count == 0 ? null : dict;
    }
}
