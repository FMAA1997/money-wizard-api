using Application.DTOs.Dashboard;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IDashboardService
{
    Task<ErrorOr<MoneyFlow>> GetMoneyFlow(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<ErrorOr<DashboardResults>> GetResults(CancellationToken cancellationToken = default);
}
