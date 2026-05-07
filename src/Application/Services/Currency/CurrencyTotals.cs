namespace Application.Services.Currency;

public sealed class CurrencyTotals
{
    private readonly CurrencyScope _scope;
    private readonly Dictionary<string, decimal> _totals;

    internal CurrencyTotals(CurrencyScope scope)
    {
        _scope = scope;
        _totals = scope.DisplayCurrencies.ToDictionary(c => c, _ => 0m);
    }

    public void Add(decimal amount, string fromCurrency, DateOnly date)
    {
        foreach (var c in _scope.DisplayCurrencies)
            _totals[c] += _scope.Convert(amount, fromCurrency, c, date);
    }

    public void AddConverted(IReadOnlyDictionary<string, decimal> perCurrency)
    {
        foreach (var c in _scope.DisplayCurrencies)
            if (perCurrency.TryGetValue(c, out var v))
                _totals[c] += v;
    }

    public IReadOnlyDictionary<string, decimal> ToDictionary() => _totals;
}
