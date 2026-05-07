using Application.Abstractions.Services;

namespace Application.Services.Currency;

public sealed class CurrencyScope
{
    private readonly CurrencyLookup _lookup;

    public CurrencyScope(CurrencyLookup lookup, IReadOnlyList<string> displayCurrencies, string primaryCurrency)
    {
        _lookup = lookup;
        DisplayCurrencies = displayCurrencies;
        PrimaryCurrency = primaryCurrency;
    }

    public IReadOnlyList<string> DisplayCurrencies { get; }
    public string PrimaryCurrency { get; }

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency, DateOnly date) =>
        _lookup.Convert(amount, fromCurrency, toCurrency, date);

    public decimal ConvertToPrimary(decimal amount, string fromCurrency, DateOnly date) =>
        _lookup.Convert(amount, fromCurrency, PrimaryCurrency, date);

    public Dictionary<string, decimal> ConvertToDisplay(decimal amount, string fromCurrency, DateOnly date) =>
        _lookup.ConvertToAll(amount, fromCurrency, DisplayCurrencies, date);

    public CurrencyTotals NewTotals() => new(this);
}
