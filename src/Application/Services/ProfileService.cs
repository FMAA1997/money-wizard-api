using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Auth;
using Application.DTOs.Profile;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using ErrorOr;

namespace Application.Services;

public sealed class ProfileService(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider,
    ICountryProfileRegistry registry,
    IUnitOfWork unitOfWork,
    UserResponseAssembler assembler) : IProfileService
{
    public async Task<ErrorOr<UserResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ProfileErrors.InvalidName;

        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return ProfileErrors.InvalidDateOfBirth;

        var country = request.Country?.ToLowerInvariant();
        if (string.IsNullOrEmpty(country) || !registry.TryGet(country, out var handler))
            return ProfileErrors.InvalidCountry;

        if (country != "ar" && request.Argentina is not null)
            return ProfileErrors.InvalidCountryPayload;

        var user = await userRepository.GetById(currentUserProvider.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        user.Name = request.Name.Trim();
        user.Dob = request.DateOfBirth;
        user.Country = country;
        userRepository.Update(user);

        var upsertResult = await handler.Upsert(user.Id, request, cancellationToken);
        if (upsertResult.IsError)
            return upsertResult.Errors;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await assembler.Build(user, cancellationToken);
    }
}
