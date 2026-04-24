using Application.Abstractions.Services;
using Application.DTOs.Investment.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/investments/statistics")]
[Authorize]
public sealed class InvestmentStatisticsController(IInvestmentStatisticsService investmentStatisticsService) : ErrorController
{
    [HttpGet("asset-class-distribution")]
    public async Task<ActionResult<AssetClassDistribution>> GetAssetClassDistribution(CancellationToken cancellationToken) =>
        MatchOk(await investmentStatisticsService.GetAssetClassDistribution(cancellationToken));
}
