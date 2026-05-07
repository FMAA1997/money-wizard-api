using Application.Abstractions.Services;

namespace Application.Services.Currency;

public sealed class CurrencyConverter(
    IUserCurrencyContext userCurrencyContext,
    IExchangeRateCache exchangeRateCache) : ICurrencyConverter
{
    public async Task<CurrencyScope> OpenScopeAsync(CancellationToken cancellationToken = default)
    {
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var profile = await userCurrencyContext.ResolveAsync(cancellationToken);
        return new CurrencyScope(lookup, profile.DisplayCurrencies, profile.PrimaryCurrency);
    }
}
