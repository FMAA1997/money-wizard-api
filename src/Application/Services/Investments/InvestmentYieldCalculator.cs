using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.Services.Currency;
using Domain.Models;

namespace Application.Services.Investments;

public sealed class InvestmentYieldCalculator : IInvestmentYieldCalculator
{
    private static readonly IReadOnlyDictionary<AssetClass, decimal> _defaultYieldsPct = new Dictionary<AssetClass, decimal>
    {
        [AssetClass.Stock] = 7m,
        [AssetClass.Etf] = 7m,
        [AssetClass.Cedear] = 7m,
        [AssetClass.Bond] = 4m,
        [AssetClass.Crypto] = 15m,
        [AssetClass.Fci] = 6m,
    };

    private const decimal _usInflationPct = 3.75m;

    public IReadOnlyDictionary<string, decimal> ComputeAnnualYield(
        IReadOnlyList<InvestmentDetailResponse> valuations,
        CurrencyScope scope)
    {
        var totals = scope.DisplayCurrencies.ToDictionary(c => c, _ => 0m);

        foreach (var inv in valuations)
        {
            if (inv.CurrentValue is null)
                continue;

            var nominalPct = inv.ManualYield ?? _defaultYieldsPct.GetValueOrDefault(inv.AssetClass);
            if (nominalPct == 0m)
                continue;

            var rate = (nominalPct - _usInflationPct) / 100m;

            foreach (var (currency, value) in inv.CurrentValue)
            {
                if (totals.ContainsKey(currency))
                    totals[currency] += value * rate;
            }
        }

        return totals;
    }
}
