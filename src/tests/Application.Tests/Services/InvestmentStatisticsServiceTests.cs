using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.Services;
using Domain.Errors;
using Domain.Models;
using ErrorOr;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class InvestmentStatisticsServiceTests
{
    private readonly Mock<IInvestmentService> _investmentServiceMock = new();
    private readonly Mock<IUserCurrencyContext> _userCurrencyContextMock = new();
    private readonly InvestmentStatisticsService _sut;

    public InvestmentStatisticsServiceTests()
    {
        _sut = new InvestmentStatisticsService(_investmentServiceMock.Object, _userCurrencyContextMock.Object);
    }

    [Fact]
    public async Task GetAssetClassDistribution_HappyPath_ReshapesAndOrdersByPrimaryCurrencyDesc()
    {
        var portfolio = new InvestmentPortfolioResponse(
            TotalCurrentValue: new Dictionary<string, decimal> { ["USD"] = 1000m, ["ARS"] = 1_000_000m },
            ByAssetClass: new List<PortfolioAssetClassBreakdown>
            {
                new(AssetClass.Stock,
                    CurrentValue: new Dictionary<string, decimal> { ["USD"] = 200m, ["ARS"] = 200_000m },
                    WeightPct: new Dictionary<string, decimal> { ["USD"] = 20m, ["ARS"] = 20m }),
                new(AssetClass.Crypto,
                    CurrentValue: new Dictionary<string, decimal> { ["USD"] = 600m, ["ARS"] = 600_000m },
                    WeightPct: new Dictionary<string, decimal> { ["USD"] = 60m, ["ARS"] = 60m }),
                new(AssetClass.Bond,
                    CurrentValue: new Dictionary<string, decimal> { ["USD"] = 200m, ["ARS"] = 200_000m },
                    WeightPct: new Dictionary<string, decimal> { ["USD"] = 20m, ["ARS"] = 20m })
            },
            UnvaluedCount: 3);

        _investmentServiceMock
            .Setup(s => s.GetPortfolio(It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        _userCurrencyContextMock
            .Setup(c => c.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserCurrencyProfile(new[] { "USD", "ARS" }, "ARS"));

        var result = await _sut.GetAssetClassDistribution();

        result.IsError.Should().BeFalse();
        result.Value.UnvaluedCount.Should().Be(3);
        result.Value.Data.Should().HaveCount(3);

        result.Value.Data[0].AssetClass.Should().Be(AssetClass.Crypto);
        result.Value.Data[0].Value["ARS"].Should().Be(600_000m);
        result.Value.Data[0].Value["USD"].Should().Be(600m);
        result.Value.Data[0].Percentage["ARS"].Should().Be(60m);
        result.Value.Data[0].Percentage["USD"].Should().Be(60m);

        result.Value.Data.Select(e => e.AssetClass).Should().ContainInOrder(
            AssetClass.Crypto, AssetClass.Stock, AssetClass.Bond);
    }

    [Fact]
    public async Task GetAssetClassDistribution_PortfolioError_PropagatesError()
    {
        _investmentServiceMock
            .Setup(s => s.GetPortfolio(It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvestmentErrors.NotFound);

        var result = await _sut.GetAssetClassDistribution();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvestmentErrors.NotFound);
        _userCurrencyContextMock.Verify(c => c.ResolveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAssetClassDistribution_EmptyPortfolio_ReturnsEmptyDataAndForwardsUnvaluedCount()
    {
        var portfolio = new InvestmentPortfolioResponse(
            TotalCurrentValue: new Dictionary<string, decimal> { ["USD"] = 0m },
            ByAssetClass: new List<PortfolioAssetClassBreakdown>(),
            UnvaluedCount: 2);

        _investmentServiceMock
            .Setup(s => s.GetPortfolio(It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        _userCurrencyContextMock
            .Setup(c => c.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserCurrencyProfile(new[] { "USD" }, "USD"));

        var result = await _sut.GetAssetClassDistribution();

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().BeEmpty();
        result.Value.UnvaluedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAssetClassDistribution_PrimaryCurrencyMissingFromEntry_TreatsAsZeroForOrdering()
    {
        var portfolio = new InvestmentPortfolioResponse(
            TotalCurrentValue: new Dictionary<string, decimal> { ["USD"] = 1000m },
            ByAssetClass: new List<PortfolioAssetClassBreakdown>
            {
                new(AssetClass.Stock,
                    CurrentValue: new Dictionary<string, decimal> { ["USD"] = 100m },
                    WeightPct: new Dictionary<string, decimal> { ["USD"] = 10m }),
                new(AssetClass.Etf,
                    CurrentValue: new Dictionary<string, decimal> { ["USD"] = 900m },
                    WeightPct: new Dictionary<string, decimal> { ["USD"] = 90m })
            },
            UnvaluedCount: 0);

        _investmentServiceMock
            .Setup(s => s.GetPortfolio(It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        _userCurrencyContextMock
            .Setup(c => c.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserCurrencyProfile(new[] { "USD" }, "USD"));

        var result = await _sut.GetAssetClassDistribution();

        result.IsError.Should().BeFalse();
        result.Value.Data.Select(e => e.AssetClass).Should().ContainInOrder(AssetClass.Etf, AssetClass.Stock);
    }
}
