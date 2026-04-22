namespace Application.Abstractions.Services;

public interface ICountryProfileRegistry
{
    bool TryGet(string country, out ICountryProfileHandler handler);

    IReadOnlyCollection<string> SupportedCountries { get; }
}
