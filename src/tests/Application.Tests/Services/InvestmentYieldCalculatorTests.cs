using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.Services.Currency;
using Application.Services.Investments;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests.Services;

public class InvestmentYieldCalculatorTests
{
    private static CurrencyScope MakeScope(params string[] currencies) =>
        new(new CurrencyLookup(new Dictionary<DateOnly, decimal>()), currencies, currencies[0]);

    private static InvestmentDetailResponse MakeValuation(
        AssetClass cls,
        IReadOnlyDictionary<string, decimal>? currentValue,
        decimal? manualYield = null) =>
        new(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AssetClass: cls,
            Ticker: "T",
            Description: "d",
            Quantity: 1m,
            Date: new DateOnly(2026, 1, 1),
            ManualYield: manualYield,
            CurrentPrice: null,
            CurrentCurrency: null,
            PriceAsOf: null,
            ValuationStatus: ValuationStatus.Live,
            CurrentValue: currentValue);

    [Fact]
    public void ComputeAnnualYield_UsesManualYieldWhenProvided()
    {
        var scope = MakeScope("USD");
        var sut = new InvestmentYieldCalculator();

        var result = sut.ComputeAnnualYield(
            new[] { MakeValuation(AssetClass.Stock, new Dictionary<string, decimal> { ["USD"] = 1000m }, manualYield: 10m) },
            scope);

        result["USD"].Should().Be(62.5m); // 1000 * (10% - 3.75% US inflation)
    }

    [Fact]
    public void ComputeAnnualYield_FallsBackToAssetClassDefault()
    {
        var scope = MakeScope("USD");
        var sut = new InvestmentYieldCalculator();

        var result = sut.ComputeAnnualYield(
            new[] { MakeValuation(AssetClass.Bond, new Dictionary<string, decimal> { ["USD"] = 1000m }, manualYield: null) },
            scope);

        result["USD"].Should().Be(2.5m); // 1000 * (4% Bond default - 3.75% US inflation)
    }

    [Fact]
    public void ComputeAnnualYield_SkipsUnvaluedInvestments()
    {
        var scope = MakeScope("USD");
        var sut = new InvestmentYieldCalculator();

        var result = sut.ComputeAnnualYield(
            new[] { MakeValuation(AssetClass.Stock, currentValue: null, manualYield: 100m) },
            scope);

        result["USD"].Should().Be(0m);
    }

    [Fact]
    public void ComputeAnnualYield_CashHasNoDefaultYield()
    {
        var scope = MakeScope("USD");
        var sut = new InvestmentYieldCalculator();

        var result = sut.ComputeAnnualYield(
            new[] { MakeValuation(AssetClass.Cash, new Dictionary<string, decimal> { ["USD"] = 1000m }, manualYield: null) },
            scope);

        result["USD"].Should().Be(0m);
    }

    [Fact]
    public void ComputeAnnualYield_AggregatesAcrossDisplayCurrencies()
    {
        var scope = MakeScope("USD", "ARS");
        var sut = new InvestmentYieldCalculator();

        var result = sut.ComputeAnnualYield(
            new[]
            {
                MakeValuation(AssetClass.Stock,
                    new Dictionary<string, decimal> { ["USD"] = 1000m, ["ARS"] = 1_000_000m },
                    manualYield: 5m)
            },
            scope);

        result["USD"].Should().Be(12.5m); // 1000 * (5% - 3.75%)
        result["ARS"].Should().Be(12_500m); // 1_000_000 * (5% - 3.75%)
    }
}
