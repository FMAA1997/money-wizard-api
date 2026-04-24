using Application.Abstractions.Services;
using Application.DTOs.Investment.Statistics;
using ErrorOr;

namespace Application.Services;

public sealed class InvestmentStatisticsService(
    IInvestmentService investmentService,
    IUserCurrencyContext userCurrencyContext) : IInvestmentStatisticsService
{
    public async Task<ErrorOr<AssetClassDistribution>> GetAssetClassDistribution(CancellationToken cancellationToken = default)
    {
        var portfolioResult = await investmentService.GetPortfolio(cancellationToken);
        if (portfolioResult.IsError)
            return portfolioResult.Errors;

        var portfolio = portfolioResult.Value;
        var primaryCurrency = (await userCurrencyContext.ResolveAsync(cancellationToken)).PrimaryCurrency;

        var data = portfolio.ByAssetClass
            .Select(b => new AssetClassDistributionEntry(
                AssetClass: b.AssetClass,
                Value: b.CurrentValue,
                Percentage: b.WeightPct))
            .OrderByDescending(e => e.Value.TryGetValue(primaryCurrency, out var v) ? v : 0m)
            .ToList();

        return new AssetClassDistribution(data, portfolio.UnvaluedCount);
    }
}
