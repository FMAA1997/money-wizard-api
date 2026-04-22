namespace Infrastructure.Services;

/// <summary>
/// Thread-safe in-memory cache with a single shared value refreshed on demand.
/// Mirrors the ExchangeRateCache pattern: lazy, time-based TTL, double-check lock,
/// graceful fallback to stale data if the refresh loader fails or returns empty.
/// </summary>
public sealed class TimedCache<TValue> where TValue : class
{
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile TValue? _value;
    private DateTimeOffset _lastLoadedAt = DateTimeOffset.MinValue;

    public TimedCache(TimeSpan ttl)
    {
        _ttl = ttl;
    }

    public async Task<TValue?> GetAsync(
        Func<CancellationToken, Task<TValue?>> loader,
        CancellationToken cancellationToken = default)
    {
        if (IsFresh())
            return _value;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
                return _value;

            TValue? loaded;
            try
            {
                loaded = await loader(cancellationToken);
            }
            catch
            {
                return _value; // graceful fallback to stale
            }

            if (loaded is null)
                return _value;

            _value = loaded;
            _lastLoadedAt = DateTimeOffset.UtcNow;
            return _value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsFresh() => _value is not null && DateTimeOffset.UtcNow - _lastLoadedAt < _ttl;
}
