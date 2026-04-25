using Application.Abstractions.Services;
using Application.DTOs.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboardService) : ErrorController
{
    [HttpGet("money-flow")]
    public async Task<ActionResult<MoneyFlow>> GetMoneyFlow(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default) =>
        MatchOk(await dashboardService.GetMoneyFlow(from, to, cancellationToken));
}
