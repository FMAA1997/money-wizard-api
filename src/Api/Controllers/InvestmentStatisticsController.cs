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

    [HttpGet("currency-distribution")]
    public async Task<ActionResult<CurrencyDistribution>> GetCurrencyDistribution(CancellationToken cancellationToken) =>
        MatchOk(await investmentStatisticsService.GetCurrencyDistribution(cancellationToken));

    [HttpGet("months-of-expenses-covered")]
    public async Task<ActionResult<MonthsOfExpensesCovered>> GetMonthsOfExpensesCovered(CancellationToken cancellationToken) =>
        MatchOk(await investmentStatisticsService.GetMonthsOfExpensesCovered(cancellationToken));

    [HttpGet("financial-independence")]
    public async Task<ActionResult<FinancialIndependence>> GetFinancialIndependence(CancellationToken cancellationToken) =>
        MatchOk(await investmentStatisticsService.GetFinancialIndependence(cancellationToken));
}
