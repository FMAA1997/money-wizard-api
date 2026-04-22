using Application.Abstractions.Services;
using Domain.Abstractions.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ExchangeRateCache : IExchangeRateCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeRateCache> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile IReadOnlyDictionary<DateOnly, decimal> _rates = new Dictionary<DateOnly, decimal>();
    private DateTimeOffset _lastLoadedAt = DateTimeOffset.MinValue;

    public ExchangeRateCache(IServiceScopeFactory scopeFactory, ILogger<ExchangeRateCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<CurrencyLookup> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFreshAsync(cancellationToken);
        return new CurrencyLookup(_rates);
    }

    private async Task EnsureFreshAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return;
            }

            await RefreshInternalAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsFresh() =>
        _rates.Count > 0 && DateTimeOffset.UtcNow - _lastLoadedAt < Ttl;

    private async Task RefreshInternalAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IExchangeRateClient>();

        var history = await client.GetMepHistoryAsync(cancellationToken);
        if (history.Count == 0)
        {
            _logger.LogWarning("ArgentinaDatos returned no MEP rate entries; keeping existing cache.");
            return;
        }

        var next = new Dictionary<DateOnly, decimal>(history.Count);
        foreach (var entry in history)
        {
            next[entry.Date] = entry.UsdToArs;
        }

        _rates = next;
        _lastLoadedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Loaded {Count} MEP rate entries (from {Min:yyyy-MM-dd} to {Max:yyyy-MM-dd}).",
            next.Count,
            next.Keys.Min(),
            next.Keys.Max());
    }
}
