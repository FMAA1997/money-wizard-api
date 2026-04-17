using Application.DTOs.Profile;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IArgentinaUserProfileService
{
    Task<ErrorOr<ArgentinaProfileResponse>> GetForCurrentUser(CancellationToken cancellationToken = default);
    Task<ErrorOr<ArgentinaProfileResponse>> Upsert(UpsertArgentinaProfileRequest request, CancellationToken cancellationToken = default);
}
