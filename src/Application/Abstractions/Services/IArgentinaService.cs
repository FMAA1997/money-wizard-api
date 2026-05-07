using Application.DTOs.Argentina.Dollars;
using Application.DTOs.Argentina.Holidays;
using Application.DTOs.Argentina.Macro;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IArgentinaService
{
    Task<ErrorOr<DollarRatesResponse>> GetDollarsAsync(CancellationToken cancellationToken = default);
    Task<ErrorOr<MacroSummary>> GetMacroAsync(CancellationToken cancellationToken = default);
    Task<ErrorOr<UpcomingHolidaysResponse>> GetUpcomingHolidaysAsync(int n, CancellationToken cancellationToken = default);
}
