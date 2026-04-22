namespace Application.Abstractions.Services;

public interface IUserCurrencyContext
{
    Task<UserCurrencyProfile> ResolveAsync(CancellationToken cancellationToken = default);
}

public sealed record UserCurrencyProfile(IReadOnlyList<string> DisplayCurrencies, string PrimaryCurrency);
