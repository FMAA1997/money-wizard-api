using Application.DTOs.Paycheck.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IPaycheckStatisticsService
{
    Task<ErrorOr<PaycheckTotals>> GetTotals(int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckMonthlyIncomeStats>> GetMonthlyIncomeStats(int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<UpcomingPaycheck>> GetUpcoming(CancellationToken cancellationToken = default);
}
