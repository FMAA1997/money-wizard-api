using Domain.Abstractions.Clients;
using Infrastructure.Services;

namespace Infrastructure.Pricing;

/// <summary>
/// In-memory singleton cache for data912 "/live/*" snapshots.
/// One cache per endpoint (us_stocks, arg_stocks, arg_cedears, arg_bonds).
/// Data is immutable for ~24h in practice — snapshots are daily.
/// </summary>
public sealed class Data912SnapshotCache(IData912Client client)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly TimedCache<IReadOnlyDictionary<string, Data912Quote>> _usStocks = new(Ttl);
    private readonly TimedCache<IReadOnlyDictionary<string, Data912Quote>> _argStocks = new(Ttl);
    private readonly TimedCache<IReadOnlyDictionary<string, Data912Quote>> _argCedears = new(Ttl);
    private readonly TimedCache<IReadOnlyDictionary<string, Data912Quote>> _argBonds = new(Ttl);

    public Task<IReadOnlyDictionary<string, Data912Quote>?> GetUsStocks(CancellationToken ct = default) =>
        _usStocks.GetAsync(async token => ToDict(await client.GetUsStocks(token)), ct);

    public Task<IReadOnlyDictionary<string, Data912Quote>?> GetArgStocks(CancellationToken ct = default) =>
        _argStocks.GetAsync(async token => ToDict(await client.GetArgStocks(token)), ct);

    public Task<IReadOnlyDictionary<string, Data912Quote>?> GetArgCedears(CancellationToken ct = default) =>
        _argCedears.GetAsync(async token => ToDict(await client.GetArgCedears(token)), ct);

    public Task<IReadOnlyDictionary<string, Data912Quote>?> GetArgBonds(CancellationToken ct = default) =>
        _argBonds.GetAsync(async token => ToDict(await client.GetArgBonds(token)), ct);

    private static IReadOnlyDictionary<string, Data912Quote>? ToDict(IReadOnlyList<Data912Quote> quotes)
    {
        if (quotes.Count == 0) return null;
        var dict = new Dictionary<string, Data912Quote>(quotes.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var q in quotes)
            dict[q.Symbol] = q;
        return dict;
    }
}
