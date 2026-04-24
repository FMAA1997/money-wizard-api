using Application.DTOs.Investment.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvestmentStatisticsService
{
    Task<ErrorOr<AssetClassDistribution>> GetAssetClassDistribution(CancellationToken cancellationToken = default);
}
