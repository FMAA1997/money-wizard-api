using System.Security.Claims;
using Api.Filters;
using Application.DTOs.Auth;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class AuthController(IAuthService authService) : ErrorController
{
    [HttpGet("sync")]
    public async Task<ActionResult<UserResponse>> Sync(CancellationToken cancellationToken)
        => MatchOk(await authService.Sync(cancellationToken));

    [HttpPost("register")]
    [SkipUserResolution]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken) =>
        MatchOk(
            await authService.Register(
                User.FindFirstValue("user_id"),
                User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
                request.Name,
                request.DateOfBirth,
                cancellationToken
            )
        );

    [HttpPatch("country")]
    public async Task<ActionResult<UserResponse>> UpdateCountry(
        [FromBody] UpdateCountryRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await authService.UpdateCountry(request.Country, cancellationToken));
}
