using Application.Abstractions;
using Application.DTOs.Auth;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork) : IAuthService
{
    public async Task<ErrorOr<UserResponse>> Sync(CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetById(currentUserProvider.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        return Map(user);
    }

    public async Task<ErrorOr<UserResponse>> Register(
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

        var existingUser = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (existingUser is not null)
            return AuthErrors.UserAlreadyExists;

        var emailTaken = await userRepository.ExistsByEmail(email, cancellationToken);
        if (emailTaken)
            return AuthErrors.EmailAlreadyInUse;

        var user = new User
        {
            ExternalId = externalId,
            Name = name,
            Email = email,
            Dob = dateOfBirth
        };

        await userRepository.Add(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(user);
    }

    private static UserResponse Map(User user)
        => new(user.Id, user.Name, user.Email, user.Dob);
}
