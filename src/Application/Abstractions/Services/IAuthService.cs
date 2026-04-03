using Application.DTOs.Auth;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IAuthService
{
    Task<ErrorOr<UserResponse>> Sync(string? externalId, CancellationToken cancellationToken = default);
    Task<ErrorOr<UserResponse>> Register(string? externalId, string? email, string name, DateOnly dateOfBirth, CancellationToken cancellationToken = default);
}
