using System.Security.Claims;
using Application.Abstractions.Services;
using Application.DTOs.Paycheck.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/paychecks/statistics")]
[Authorize]
public sealed class PaycheckStatisticsController(IPaycheckStatisticsService paycheckStatisticsService) : ErrorController
{
    [HttpGet("totals")]
    public async Task<ActionResult<PaycheckTotals>> GetTotals([FromQuery] int? year = null, CancellationToken cancellationToken = default) =>
        MatchOk(await paycheckStatisticsService.GetTotals(
            User.FindFirstValue("user_id"),
            year ?? DateTime.UtcNow.Year,
            cancellationToken
        ));

    [HttpGet("monthly-income")]
    public async Task<ActionResult<PaycheckMonthlyIncomeStats>> GetMonthlyIncomeStats([FromQuery] int? year = null, CancellationToken cancellationToken = default) =>
        MatchOk(await paycheckStatisticsService.GetMonthlyIncomeStats(
            User.FindFirstValue("user_id"),
            year ?? DateTime.UtcNow.Year,
            cancellationToken
        ));

    [HttpGet("upcoming")]
    public async Task<ActionResult<UpcomingPaycheck>> GetUpcoming(CancellationToken cancellationToken) =>
        MatchOk(await paycheckStatisticsService.GetUpcoming(
            User.FindFirstValue("user_id"),
            cancellationToken
        ));
}
