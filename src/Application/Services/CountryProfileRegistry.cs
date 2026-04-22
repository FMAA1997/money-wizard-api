using Application.Abstractions.Services;

namespace Application.Services;

public sealed class CountryProfileRegistry : ICountryProfileRegistry
{
    private readonly Dictionary<string, ICountryProfileHandler> _handlers;

    public CountryProfileRegistry(IEnumerable<ICountryProfileHandler> handlers)
    {
        _handlers = new Dictionary<string, ICountryProfileHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            if (!_handlers.TryAdd(handler.CountryCode, handler))
                throw new InvalidOperationException(
                    $"Duplicate ICountryProfileHandler registration for country '{handler.CountryCode}'.");
        }
    }

    public bool TryGet(string country, out ICountryProfileHandler handler)
    {
        if (!string.IsNullOrWhiteSpace(country) &&
            _handlers.TryGetValue(country, out var found))
        {
            handler = found;
            return true;
        }

        handler = null!;
        return false;
    }

    public IReadOnlyCollection<string> SupportedCountries => _handlers.Keys;
}
