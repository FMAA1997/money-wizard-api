using Application.Abstractions.Services;
using Application.DTOs.Auth;
using Application.DTOs.Profile;
using Domain.Models;

namespace Application.Services;

public sealed class UserResponseAssembler(ICountryProfileRegistry registry)
{
    public async Task<UserResponse> Build(User user, CancellationToken cancellationToken = default)
    {
        var hasHandler = registry.TryGet(user.Country, out var handler)
            || registry.TryGet("row", out handler);

        var slice = hasHandler ? await handler!.LoadResponseSlice(user.Id, cancellationToken) : null;

        return new UserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Dob,
            user.Country,
            DisplayCurrencies: handler?.DisplayCurrencies ?? [],
            PrimaryCurrency: handler?.PrimaryCurrency ?? "USD",
            ArgentinaProfile: slice as ArgentinaProfileResponse);
    }
}
