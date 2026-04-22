using Application.DTOs.Auth;
using Application.DTOs.Profile;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IProfileService
{
    Task<ErrorOr<UserResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
