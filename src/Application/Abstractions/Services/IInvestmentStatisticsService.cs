using Application.DTOs.Investment.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvestmentStatisticsService
{
    Task<ErrorOr<AssetClassDistribution>> GetAssetClassDistribution(CancellationToken cancellationToken = default);
    Task<ErrorOr<CurrencyDistribution>> GetCurrencyDistribution(CancellationToken cancellationToken = default);
    Task<ErrorOr<MonthsOfExpensesCovered>> GetMonthsOfExpensesCovered(CancellationToken cancellationToken = default);
    Task<ErrorOr<FinancialIndependence>> GetFinancialIndependence(CancellationToken cancellationToken = default);
    Task<ErrorOr<SavingsRate>> GetSavingsRate(CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpensesCoveredByYield>> GetExpensesCoveredByYield(CancellationToken cancellationToken = default);
}
