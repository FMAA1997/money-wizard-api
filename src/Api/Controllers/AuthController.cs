using System.Security.Claims;
using Application.DTOs.Auth;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class AuthController(IAuthService authService) : ErrorController
{
    [HttpPost("sync")]
    public async Task<ActionResult<UserResponse>> Sync(CancellationToken cancellationToken)
        => MatchOk(await authService.SyncUserAsync(
            User.FindFirstValue("user_id"), cancellationToken));

    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await authService.RegisterUserAsync(
            User.FindFirstValue("user_id"),
            User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
            request.Name,
            request.DateOfBirth,
            cancellationToken));
}
