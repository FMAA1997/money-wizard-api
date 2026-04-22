using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Auth;
using Application.DTOs.Profile;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider,
    ICountryProfileRegistry registry,
    IUnitOfWork unitOfWork,
    UserResponseAssembler assembler) : IAuthService
{
    public async Task<ErrorOr<UserResponse>> Sync(CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetById(currentUserProvider.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        return await assembler.Build(user, cancellationToken);
    }

    public async Task<ErrorOr<UserResponse>> Register(
        string? externalId,
        string? email,
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        if (string.IsNullOrEmpty(email))
            return AuthErrors.MissingEmail;

        if (string.IsNullOrWhiteSpace(request.Name))
            return ProfileErrors.InvalidName;

        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return ProfileErrors.InvalidDateOfBirth;

        var country = request.Country?.ToLowerInvariant();
        if (string.IsNullOrEmpty(country) || !registry.TryGet(country, out var handler))
            return ProfileErrors.InvalidCountry;

        if (country != "ar" && request.Argentina is not null)
            return ProfileErrors.InvalidCountryPayload;

        var existingUser = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (existingUser is not null)
            return AuthErrors.UserAlreadyExists;

        var emailTaken = await userRepository.ExistsByEmail(email, cancellationToken);
        if (emailTaken)
            return AuthErrors.EmailAlreadyInUse;

        var user = new User
        {
            ExternalId = externalId,
            Name = request.Name.Trim(),
            Email = email,
            Dob = request.DateOfBirth,
            Country = country
        };

        await userRepository.Add(user, cancellationToken);

        var upsertRequest = new UpdateProfileRequest(
            request.Name,
            request.DateOfBirth,
            country,
            request.Argentina);

        var upsertResult = await handler.Upsert(user.Id, upsertRequest, cancellationToken);
        if (upsertResult.IsError)
            return upsertResult.Errors;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await assembler.Build(user, cancellationToken);
    }
}
