using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Application.Services;
using Application.Tests.Helpers;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using ErrorOr;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class InvestmentStatisticsServiceTests
{
    private readonly Mock<IInvestmentService> _investmentServiceMock = new();
    private readonly Mock<IExpenseRepository> _expenseRepositoryMock = new();
    private readonly Mock<IPaycheckRepository> _paycheckRepositoryMock = new();
    private readonly Mock<IInvoiceRepository> _invoiceRepositoryMock = new();
    private readonly Mock<IInvestmentYieldCalculator> _yieldCalculatorMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();

    private InvestmentStatisticsService Build(string[]? displayCurrencies = null, string primary = "ARS") =>
        new(
            _investmentServiceMock.Object,
            TestCurrencyConverter.Create(displayCurrencies: displayCurrencies ?? new[] { "USD", "ARS" }, primaryCurrency: primary),
            _expenseRepositoryMock.Object,
            _paycheckRepositoryMock.Object,
            _invoiceRepositoryMock.Object,
            _yieldCalculatorMock.Object,
            _currentUserProviderMock.Object);

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

        var sut = Build();
        var result = await sut.GetAssetClassDistribution();

        result.IsError.Should().BeFalse();
        result.Value.UnvaluedCount.Should().Be(3);
        result.Value.Data.Should().HaveCount(3);

        result.Value.Data[0].AssetClass.Should().Be(AssetClass.Crypto);
        result.Value.Data[0].Value["ARS"].Should().Be(600_000m);
        result.Value.Data[0].Value["USD"].Should().Be(600m);

        result.Value.Data.Select(e => e.AssetClass).Should().ContainInOrder(
            AssetClass.Crypto, AssetClass.Stock, AssetClass.Bond);
    }

    [Fact]
    public async Task GetAssetClassDistribution_PortfolioError_PropagatesError()
    {
        _investmentServiceMock
            .Setup(s => s.GetPortfolio(It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvestmentErrors.NotFound);

        var sut = Build();
        var result = await sut.GetAssetClassDistribution();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvestmentErrors.NotFound);
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

        var sut = Build(displayCurrencies: new[] { "USD" }, primary: "USD");
        var result = await sut.GetAssetClassDistribution();

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().BeEmpty();
        result.Value.UnvaluedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFinancialIndependence_HappyPath_ComputesThreeMilestones()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oneMonthAgo = today.AddMonths(-1);

        _investmentServiceMock
            .Setup(s => s.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentDetailResponse>().AsReadOnly());

        _yieldCalculatorMock
            .Setup(c => c.ComputeAnnualYield(It.IsAny<IReadOnlyList<InvestmentDetailResponse>>(), It.IsAny<Application.Services.Currency.CurrencyScope>()))
            .Returns(new Dictionary<string, decimal> { ["USD"] = 600m, ["ARS"] = 600_000m });

        _expenseRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Expense>
            {
                new() { UserId = Guid.NewGuid(), Date = oneMonthAgo, Amount = 100m, Currency = "USD", Description = "rent" }
            });

        _paycheckRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Paycheck>
            {
                new() { UserId = Guid.NewGuid(), Date = oneMonthAgo, Amount = 500m, Currency = "USD", Description = "salary" }
            });

        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Invoice>());

        var sut = Build(displayCurrencies: new[] { "USD" }, primary: "USD");
        var result = await sut.GetFinancialIndependence();

        result.IsError.Should().BeFalse();
        result.Value.Months.Should().Be(2);
        result.Value.AnnualYield["USD"].Should().Be(600m);

        // 2 months of data → annualizationFactor = 6
        // ttm expenses = 100 USD → annual = 600
        // ttm income = 500 USD → annual = 3000
        // ttm savings = (income - expenses) annualized = 3000 - 600 = 2400
        result.Value.Expenses.Target["USD"].Should().Be(600m);
        result.Value.Income.Target["USD"].Should().Be(3000m);
        result.Value.Savings.Target["USD"].Should().Be(2400m);

        result.Value.Expenses.Coverage["USD"].Should().Be(1m);             // 600 / 600
        result.Value.Income.Coverage["USD"].Should().Be(0.2m);             // 600 / 3000
        result.Value.Savings.Coverage["USD"].Should().Be(0.25m);           // 600 / 2400
    }

    [Fact]
    public async Task GetFinancialIndependence_NegativeSavings_CoverageIsNull()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _investmentServiceMock
            .Setup(s => s.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentDetailResponse>().AsReadOnly());

        _yieldCalculatorMock
            .Setup(c => c.ComputeAnnualYield(It.IsAny<IReadOnlyList<InvestmentDetailResponse>>(), It.IsAny<Application.Services.Currency.CurrencyScope>()))
            .Returns(new Dictionary<string, decimal> { ["USD"] = 100m });

        // expenses > income → negative savings
        _expenseRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Expense>
            {
                new() { UserId = Guid.NewGuid(), Date = today, Amount = 1000m, Currency = "USD", Description = "x" }
            });
        _paycheckRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Paycheck>
            {
                new() { UserId = Guid.NewGuid(), Date = today, Amount = 500m, Currency = "USD", Description = "y" }
            });
        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Invoice>());

        var sut = Build(displayCurrencies: new[] { "USD" }, primary: "USD");
        var result = await sut.GetFinancialIndependence();

        result.IsError.Should().BeFalse();
        result.Value.Savings.Coverage["USD"].Should().BeNull();
    }

    [Fact]
    public async Task GetFinancialIndependence_NoData_MonthsIsZeroAndCoveragesAreNull()
    {
        _investmentServiceMock
            .Setup(s => s.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentDetailResponse>().AsReadOnly());

        _yieldCalculatorMock
            .Setup(c => c.ComputeAnnualYield(It.IsAny<IReadOnlyList<InvestmentDetailResponse>>(), It.IsAny<Application.Services.Currency.CurrencyScope>()))
            .Returns(new Dictionary<string, decimal> { ["USD"] = 1000m });

        _expenseRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Expense>());
        _paycheckRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Paycheck>());
        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Invoice>());

        var sut = Build(displayCurrencies: new[] { "USD" }, primary: "USD");
        var result = await sut.GetFinancialIndependence();

        result.IsError.Should().BeFalse();
        result.Value.Months.Should().Be(0);
        result.Value.Expenses.Coverage["USD"].Should().BeNull();
        result.Value.Income.Coverage["USD"].Should().BeNull();
        result.Value.Savings.Coverage["USD"].Should().BeNull();
    }
}
