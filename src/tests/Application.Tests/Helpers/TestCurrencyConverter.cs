using Application.Abstractions.Services;
using Application.Services.Currency;

namespace Application.Tests.Helpers;

internal static class TestCurrencyConverter
{
    public static ICurrencyConverter Create(
        IReadOnlyDictionary<DateOnly, decimal>? usdToArsRates = null,
        IReadOnlyList<string>? displayCurrencies = null,
        string primaryCurrency = "USD")
    {
        var lookup = new CurrencyLookup(usdToArsRates ?? new Dictionary<DateOnly, decimal>());
        var scope = new CurrencyScope(lookup, displayCurrencies ?? new[] { primaryCurrency }, primaryCurrency);
        return new StubConverter(scope);
    }

    private sealed class StubConverter(CurrencyScope scope) : ICurrencyConverter
    {
        public Task<CurrencyScope> OpenScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(scope);
    }
}
