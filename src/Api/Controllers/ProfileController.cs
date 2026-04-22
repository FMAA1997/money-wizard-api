using Application.Abstractions.Services;
using Application.DTOs.Auth;
using Application.DTOs.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class ProfileController(IProfileService profileService) : ErrorController
{
    [HttpPut]
    public async Task<ActionResult<UserResponse>> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await profileService.UpdateProfile(request, cancellationToken));
}
