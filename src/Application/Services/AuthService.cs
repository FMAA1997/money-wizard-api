using Application.DTOs.Auth;
using Domain.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class AuthService(IUserRepository userRepository) : IAuthService
{
    public async Task<ErrorOr<UserResponse>> SyncUserAsync(string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalIdAsync(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        return Map(user);
    }

    public async Task<ErrorOr<UserResponse>> RegisterUserAsync(
        string? externalId,
        string? email,
        string name,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        if (string.IsNullOrEmpty(email))
            return AuthErrors.MissingEmail;

        var existingUser = await userRepository.GetByExternalIdAsync(externalId, cancellationToken);
        if (existingUser is not null)
            return AuthErrors.UserAlreadyExists;

        var emailTaken = await userRepository.ExistsByEmailAsync(email, cancellationToken);
        if (emailTaken)
            return AuthErrors.EmailAlreadyInUse;

        var user = new User
        {
            ExternalId = externalId,
            Name = name,
            Email = email,
            Dob = dateOfBirth
        };

        await userRepository.AddAsync(user, cancellationToken);

        return Map(user);
    }

    private static UserResponse Map(User user)
        => new(user.Id, user.Name, user.Email, user.Dob);
}
