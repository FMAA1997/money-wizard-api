using Application.Abstractions;
using Application.Services;
using Application.Tests.Helpers;
using Domain.Abstractions.Repositories;
using Domain.Models;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class DashboardServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly YearStart = new(Today.Year, 1, 1);

    private readonly Mock<IPaycheckRepository> _paycheckRepository = new();
    private readonly Mock<IInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IExpenseRepository> _expenseRepository = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _currentUser.Setup(p => p.UserId).Returns(UserId);
        SetupPaychecks();
        SetupInvoices(hasInvoices: false);
        SetupExpenses();

        _sut = new DashboardService(
            _paycheckRepository.Object,
            _invoiceRepository.Object,
            _expenseRepository.Object,
            _currentUser.Object,
            TestCurrencyConverter.Create());
    }

    [Fact]
    public async Task GetResults_NoInvoiceUser_PopulatesPrimaryOnly()
    {
        var date = YearStart.AddDays(5);
        SetupPaychecks(BuildPaycheck(amount: 1000m, date));
        SetupExpenses(BuildExpense(amount: 300m, date));

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        var ytd = result.Value.YearToDate;
        ytd.TotalPaychecks["USD"].Should().Be(1000m);
        ytd.TotalExpenses["USD"].Should().Be(300m);
        ytd.PrimaryResult["USD"].Should().Be(700m);
        ytd.TotalInvoiced.Should().BeNull();
        ytd.NonInvoicedTotal.Should().BeNull();
        ytd.FinalResult.Should().BeNull();
    }

    [Fact]
    public async Task GetResults_InvoiceUser_PopulatesAllMetrics()
    {
        var date = YearStart.AddDays(5);
        SetupPaychecks(BuildPaycheck(amount: 1500m, date));
        SetupInvoices(hasInvoices: true, BuildInvoice(InvoiceType.Invoice, 1000m, date));
        SetupExpenses(BuildExpense(amount: 300m, date));

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        var ytd = result.Value.YearToDate;
        ytd.TotalPaychecks["USD"].Should().Be(1500m);
        ytd.TotalExpenses["USD"].Should().Be(300m);
        ytd.TotalInvoiced.Should().NotBeNull();
        ytd.TotalInvoiced!["USD"].Should().Be(1000m);
        ytd.PrimaryResult["USD"].Should().Be(700m);     // invoiced - expenses
        ytd.NonInvoicedTotal!["USD"].Should().Be(500m); // paychecks - invoiced
        ytd.FinalResult!["USD"].Should().Be(1200m);     // paychecks - expenses
    }

    [Fact]
    public async Task GetResults_CreditNote_OffsetsInvoicedAmount()
    {
        var date = YearStart.AddDays(5);
        SetupInvoices(
            hasInvoices: true,
            BuildInvoice(InvoiceType.Invoice, 1000m, date),
            BuildInvoice(InvoiceType.CreditNote, 200m, date));

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        result.Value.YearToDate.TotalInvoiced!["USD"].Should().Be(800m);
    }

    [Fact]
    public async Task GetResults_HasInvoicesButNoneInYear_StillReturnsInvoiceShape()
    {
        // User registered as having invoices, but none fall in the current year.
        SetupInvoices(hasInvoices: true);
        SetupPaychecks(BuildPaycheck(1000m, YearStart.AddDays(5)));
        SetupExpenses(BuildExpense(300m, YearStart.AddDays(5)));

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        var ytd = result.Value.YearToDate;
        ytd.TotalInvoiced.Should().NotBeNull();
        ytd.TotalInvoiced!["USD"].Should().Be(0m);
        ytd.PrimaryResult["USD"].Should().Be(-300m);    // 0 invoiced - 300 expenses
        ytd.NonInvoicedTotal!["USD"].Should().Be(1000m);
        ytd.FinalResult!["USD"].Should().Be(700m);
    }

    [Fact]
    public async Task GetResults_RecurringMonthlyPaycheck_ProjectsFullYear()
    {
        var series = BuildRecurringPaycheck(amount: 100m, startDate: YearStart, totalInstallments: 12);
        SetupPaychecks(series);

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        result.Value.YearProjected.TotalPaychecks["USD"].Should().Be(1200m);
        // YTD: occurrences on the 1st of each month from Jan through today's month.
        result.Value.YearToDate.TotalPaychecks["USD"].Should().Be(Today.Month * 100m);
        // CurrentMonth: exactly one occurrence (the 1st of this month).
        result.Value.CurrentMonth.TotalPaychecks["USD"].Should().Be(100m);
    }

    [Fact]
    public async Task GetResults_RecurrenceException_OverridesAmount()
    {
        var series = BuildRecurringPaycheck(amount: 100m, startDate: YearStart, totalInstallments: 12);
        series.Exceptions.Add(new PaycheckException
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            OriginalDate = YearStart.AddMonths(1),
            Date = YearStart.AddMonths(1),
            Amount = 250m,
            Currency = "USD",
        });
        SetupPaychecks(series);

        var result = await _sut.GetResults();

        // 11 regular months at 100 + 1 override at 250 = 1350 across full year.
        result.IsError.Should().BeFalse();
        result.Value.YearProjected.TotalPaychecks["USD"].Should().Be(1350m);
    }

    [Fact]
    public async Task GetResults_OutsideCurrentMonthDate_NotIncludedInCurrentMonth()
    {
        // Place a paycheck in a different month than today.
        var paycheckDate = Today.Month == 1 ? new DateOnly(Today.Year, 2, 1) : YearStart;
        SetupPaychecks(BuildPaycheck(1000m, paycheckDate));

        var result = await _sut.GetResults();

        result.IsError.Should().BeFalse();
        result.Value.CurrentMonth.TotalPaychecks["USD"].Should().Be(0m);
        result.Value.CurrentMonth.PrimaryResult["USD"].Should().Be(0m);
    }

    private void SetupPaychecks(params PaycheckSeries[] paychecks) =>
        _paycheckRepository
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paychecks);

    private void SetupInvoices(bool hasInvoices, params InvoiceSeries[] invoices)
    {
        _invoiceRepository
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);
        _invoiceRepository
            .Setup(r => r.AnyForUser(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasInvoices);
    }

    private void SetupExpenses(params ExpenseSeries[] expenses) =>
        _expenseRepository
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expenses);

    private static PaycheckSeries BuildPaycheck(decimal amount, DateOnly date)
    {
        var series = new PaycheckSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Salary",
        };
        series.Segments.Add(new PaycheckSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = amount,
            Currency = "USD",
        });
        return series;
    }

    private static PaycheckSeries BuildRecurringPaycheck(decimal amount, DateOnly startDate, int totalInstallments)
    {
        var series = new PaycheckSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Salary",
        };
        series.Segments.Add(new PaycheckSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            EffectiveFrom = startDate,
            Amount = amount,
            Currency = "USD",
            RecurrenceRule = new RecurrenceRule
            {
                Frequency = RecurrenceFrequency.Monthly,
                Interval = 1,
                TotalInstallments = totalInstallments,
            },
        });
        return series;
    }

    private static InvoiceSeries BuildInvoice(InvoiceType type, decimal amount, DateOnly date)
    {
        var series = new InvoiceSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = type.ToString(),
            Type = type,
        };
        series.Segments.Add(new InvoiceSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = amount,
            Currency = "USD",
        });
        return series;
    }

    private static ExpenseSeries BuildExpense(decimal amount, DateOnly date)
    {
        var series = new ExpenseSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Expense",
        };
        series.Segments.Add(new ExpenseSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = amount,
            Currency = "USD",
        });
        return series;
    }
}
