namespace Application.Abstractions.Services;

public interface IExchangeRateCache
{
    Task<CurrencyLookup> GetLookupAsync(CancellationToken cancellationToken = default);
}

public sealed class CurrencyLookup(IReadOnlyDictionary<DateOnly, decimal> usdToArsRates)
{
    private const int _maxFallbackDays = 10;
    private readonly DateOnly? _usdToArsLatestDate = usdToArsRates.Count == 0 ? null : usdToArsRates.Keys.Max();

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency, DateOnly date)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        if (IsUsd(fromCurrency) && IsArs(toCurrency))
            return amount * GetUsdToArsRate(date);

        if (IsArs(fromCurrency) && IsUsd(toCurrency))
            return amount / GetUsdToArsRate(date);

        throw new NotSupportedException(
            $"No exchange rate available for {fromCurrency} -> {toCurrency}.");
    }

    public Dictionary<string, decimal> ConvertToAll(
        decimal amount,
        string fromCurrency,
        IEnumerable<string> targetCurrencies,
        DateOnly date)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [fromCurrency] = amount
        };

        foreach (var target in targetCurrencies)
        {
            if (result.ContainsKey(target))
                continue;

            result[target] = Convert(amount, fromCurrency, target, date);
        }

        return result;
    }

    private decimal GetUsdToArsRate(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (date > today && _usdToArsLatestDate.HasValue)
        {
            return usdToArsRates[_usdToArsLatestDate.Value];
        }

        for (var i = 0; i < _maxFallbackDays; i++)
        {
            if (usdToArsRates.TryGetValue(date.AddDays(-i), out var rate))
            {
                return rate;
            }
        }

        throw new InvalidOperationException(
            $"No USD/ARS MEP rate available for {date:yyyy-MM-dd} or the {_maxFallbackDays} preceding days.");
    }

    private static bool IsUsd(string currency) =>
        string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);

    private static bool IsArs(string currency) =>
        string.Equals(currency, "ARS", StringComparison.OrdinalIgnoreCase);
}
