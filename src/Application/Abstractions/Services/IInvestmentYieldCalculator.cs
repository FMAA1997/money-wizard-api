using Application.DTOs.Investment;
using Application.Services.Currency;

namespace Application.Abstractions.Services;

public interface IInvestmentYieldCalculator
{
    IReadOnlyDictionary<string, decimal> ComputeAnnualYield(
        IReadOnlyList<InvestmentDetailResponse> valuations,
        CurrencyScope scope);
}
