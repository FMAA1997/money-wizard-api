using Application.Abstractions.Pricing;
using Domain.Abstractions.Clients;
using Domain.Models;
using ErrorOr;

namespace Infrastructure.Pricing.Providers;

internal sealed class ArgentinaDatosFciProvider(ArgentinaDatosFciSnapshotCache cache) : IPriceProvider
{
    private const int EmptyQueryLimit = 15;
    private const int MatchLimit = 25;

    public AssetClass AssetClass => AssetClass.Fci;

    public async Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default)
    {
        var key = (ticker ?? string.Empty).Trim();
        if (key.Length == 0)
            return Error.Validation("Pricing.TickerRequired", "Ticker is required.");

        var snapshot = await cache.GetAll(cancellationToken);
        if (snapshot is null)
            return Error.Failure("Pricing.Upstream", "Price data unavailable.");

        if (!snapshot.TryGetValue(key, out var quote))
            return Error.NotFound("Pricing.TickerNotFound", $"Ticker '{key}' not found.");

        return new AssetPrice(quote.Ticker, quote.CuotaParte, quote.Currency, quote.AsOf);
    }

    public async Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default)
    {
        var snapshot = await cache.GetAll(cancellationToken);
        if (snapshot is null)
            return Array.Empty<AssetSearchResult>();

        var q = (query ?? string.Empty).Trim();

        IEnumerable<FciQuote> matches = q.Length == 0
            ? snapshot.Values
            : snapshot.Values.Where(v => v.FundName.Contains(q, StringComparison.OrdinalIgnoreCase));

        return matches
            .OrderBy(v => v.FundName, StringComparer.OrdinalIgnoreCase)
            .Take(q.Length == 0 ? EmptyQueryLimit : MatchLimit)
            .Select(v => new AssetSearchResult(v.Ticker, $"{v.FundName} ({v.Category})", v.Currency))
            .ToList();
    }
}
