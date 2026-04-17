using Application.Abstractions.Services;
using Application.DTOs.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/profile/argentina")]
[Authorize]
public sealed class ArgentinaProfileController(IArgentinaUserProfileService profileService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<ArgentinaProfileResponse>> Get(CancellationToken cancellationToken)
        => MatchOk(await profileService.GetForCurrentUser(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ArgentinaProfileResponse>> Upsert(
        [FromBody] UpsertArgentinaProfileRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await profileService.Upsert(request, cancellationToken));
}
