using Application.DTOs.Auth;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IAuthService
{
    Task<ErrorOr<UserResponse>> SyncUserAsync(string? externalId, CancellationToken cancellationToken = default);
    Task<ErrorOr<UserResponse>> RegisterUserAsync(string? externalId, string? email, string name, DateOnly dateOfBirth, CancellationToken cancellationToken = default);
}
