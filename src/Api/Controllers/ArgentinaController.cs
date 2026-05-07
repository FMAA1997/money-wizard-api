using Application.Abstractions.Services;
using Application.DTOs.Argentina.Dollars;
using Application.DTOs.Argentina.Holidays;
using Application.DTOs.Argentina.Macro;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/argentina")]
[Authorize]
public sealed class ArgentinaController(IArgentinaService argentinaService) : ErrorController
{
    [HttpGet("dollars")]
    public async Task<ActionResult<DollarRatesResponse>> GetDollars(CancellationToken cancellationToken) =>
        MatchOk(await argentinaService.GetDollarsAsync(cancellationToken));

    [HttpGet("macro")]
    public async Task<ActionResult<MacroSummary>> GetMacro(CancellationToken cancellationToken) =>
        MatchOk(await argentinaService.GetMacroAsync(cancellationToken));

    [HttpGet("holidays")]
    public async Task<ActionResult<UpcomingHolidaysResponse>> GetHolidays(
        [FromQuery] int n = 5,
        CancellationToken cancellationToken = default) =>
        MatchOk(await argentinaService.GetUpcomingHolidaysAsync(n, cancellationToken));
}
