using Application.Abstractions;
using Application.Abstractions.Services;
using Domain.Abstractions.Repositories;

namespace Application.Services;

public sealed class UserCurrencyContext(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider,
    ICountryProfileRegistry registry) : IUserCurrencyContext
{
    private const string FallbackCountry = "row";

    public async Task<UserCurrencyProfile> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetById(currentUserProvider.UserId, cancellationToken);
        var country = user?.Country ?? FallbackCountry;

        if (!registry.TryGet(country, out var handler) && !registry.TryGet(FallbackCountry, out handler))
        {
            throw new InvalidOperationException(
                $"No country profile handler registered for '{country}' or fallback '{FallbackCountry}'.");
        }

        return new UserCurrencyProfile(handler.DisplayCurrencies, handler.PrimaryCurrency);
    }
}
