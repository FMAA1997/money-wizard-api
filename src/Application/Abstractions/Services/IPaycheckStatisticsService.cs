using Application.DTOs.Paycheck.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IPaycheckStatisticsService
{
    Task<ErrorOr<PaycheckTotals>> GetTotals(string? externalId, int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckMonthlyIncomeStats>> GetMonthlyIncomeStats(string? externalId, int year, CancellationToken cancellationToken = default);
    Task<ErrorOr<UpcomingPaycheck>> GetUpcoming(string? externalId, CancellationToken cancellationToken = default);
}
