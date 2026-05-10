using Application.Abstractions;
using Application.Abstractions.Services;
using Application.Services;
using Application.Tests.Helpers;
using Domain.Abstractions.Repositories;
using Domain.Models;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class InvoiceStatisticsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IInvoiceRepository> _invoiceRepositoryMock = new();
    private readonly Mock<IInvoiceCategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<ICountryProfileRegistry> _registryMock = new();
    private readonly Mock<ICountryProfileHandler> _handlerMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();
    private readonly InvoiceStatisticsService _sut;

    public InvoiceStatisticsServiceTests()
    {
        _currentUserProviderMock.Setup(p => p.UserId).Returns(UserId);

        _userRepositoryMock
            .Setup(r => r.GetById(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = UserId,
                ExternalId = "ext",
                Name = "n",
                Email = "e@e.com",
                Country = "ar"
            });

        var handler = _handlerMock.Object;
        _registryMock
            .Setup(r => r.TryGet("ar", out handler!))
            .Returns(true);

        _handlerMock
            .Setup(h => h.GetInvoiceCategoryId(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CategoryId);

        _categoryRepositoryMock
            .Setup(r => r.GetById(CategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceCategory
            {
                Id = CategoryId,
                Name = "Responsable Inscripto",
                Country = "ar",
                Type = "T",
                CutDate = new DateOnly(2000, 1, 1),
                Bottom = 0m,
                Top = 10_000_000m,
                Tax = 0m
            });

        _sut = new InvoiceStatisticsService(
            _userRepositoryMock.Object,
            _invoiceRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _registryMock.Object,
            _currentUserProviderMock.Object,
            TestCurrencyConverter.Create());
    }

    [Fact]
    public async Task GetCategoryProgress_CreditNoteOneOff_SubtractsFromInvoicedAmount()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodDate = new DateOnly(today.Year, today.Month, 1);

        var invoice = BuildOneOff(InvoiceType.Invoice, amount: 1000m, date: periodDate);
        var creditNote = BuildOneOff(InvoiceType.CreditNote, amount: 200m, date: periodDate);
        SetupInvoices(invoice, creditNote);

        var result = await _sut.GetCategoryProgress();

        result.IsError.Should().BeFalse();
        result.Value.InvoicedAmount.Should().Be(800m);
        result.Value.ProjectedAmount.Should().Be(800m);
    }

    [Fact]
    public async Task GetCategoryProgress_DebitNoteOneOff_AddsToInvoicedAmount()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodDate = new DateOnly(today.Year, today.Month, 1);

        var invoice = BuildOneOff(InvoiceType.Invoice, amount: 1000m, date: periodDate);
        var debitNote = BuildOneOff(InvoiceType.DebitNote, amount: 150m, date: periodDate);
        SetupInvoices(invoice, debitNote);

        var result = await _sut.GetCategoryProgress();

        result.IsError.Should().BeFalse();
        result.Value.InvoicedAmount.Should().Be(1150m);
    }

    [Fact]
    public async Task GetCategoryProgress_RecurringCreditNote_AllOccurrencesSubtract()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = new DateOnly(today.Year, 1, 1);

        var invoice = BuildOneOff(InvoiceType.Invoice, amount: 1000m, date: periodStart);
        var creditSeries = new InvoiceSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Monthly CN",
            Type = InvoiceType.CreditNote,
        };
        creditSeries.Segments.Add(new InvoiceSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = creditSeries.Id,
            EffectiveFrom = periodStart,
            Amount = 100m,
            Currency = "ARS",
            RecurrenceRule = new RecurrenceRule
            {
                Frequency = RecurrenceFrequency.Monthly,
                Interval = 1,
                TotalInstallments = 3,
            },
        });
        SetupInvoices(invoice, creditSeries);

        var result = await _sut.GetCategoryProgress();

        result.IsError.Should().BeFalse();
        result.Value.ProjectedAmount.Should().Be(700m);
    }

    private static InvoiceSeries BuildOneOff(InvoiceType type, decimal amount, DateOnly date)
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
            Currency = "ARS",
        });
        return series;
    }

    private void SetupInvoices(params InvoiceSeries[] invoices)
    {
        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);
    }
}
