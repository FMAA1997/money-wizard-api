using Application.Services.Currency;

namespace Application.Abstractions.Services;

public interface ICurrencyConverter
{
    Task<CurrencyScope> OpenScopeAsync(CancellationToken cancellationToken = default);
}
