using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.Services.Currency;
using Domain.Models;

namespace Application.Services.Investments;

public sealed class InvestmentYieldCalculator : IInvestmentYieldCalculator
{
    private static readonly IReadOnlyDictionary<AssetClass, decimal> DefaultYieldsPct = new Dictionary<AssetClass, decimal>
    {
        [AssetClass.Stock] = 7m,
        [AssetClass.Etf] = 7m,
        [AssetClass.Cedear] = 7m,
        [AssetClass.Bond] = 4m,
        [AssetClass.Crypto] = 15m,
        [AssetClass.Fci] = 6m,
    };

    public IReadOnlyDictionary<string, decimal> ComputeAnnualYield(
        IReadOnlyList<InvestmentDetailResponse> valuations,
        CurrencyScope scope)
    {
        var totals = scope.DisplayCurrencies.ToDictionary(c => c, _ => 0m);

        foreach (var inv in valuations)
        {
            if (inv.CurrentValue is null)
                continue;

            var rate = (inv.ManualYield ?? DefaultYieldsPct.GetValueOrDefault(inv.AssetClass)) / 100m;
            if (rate == 0m)
                continue;

            foreach (var (currency, value) in inv.CurrentValue)
            {
                if (totals.ContainsKey(currency))
                    totals[currency] += value * rate;
            }
        }

        return totals;
    }
}
