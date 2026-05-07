using Application.Abstractions;
using Application.Services;
using Application.Tests.Helpers;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class InvoiceServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly Mock<IInvoiceRepository> _invoiceRepositoryMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly InvoiceService _sut;

    public InvoiceServiceTests()
    {
        _currentUserProviderMock.Setup(p => p.UserId).Returns(UserId);

        _sut = new InvoiceService(
            _invoiceRepositoryMock.Object,
            _currentUserProviderMock.Object,
            _unitOfWorkMock.Object,
            TestCurrencyConverter.Create());
    }

    [Fact]
    public async Task GetCalendar_RecurringInvoiceWithNumber_ExpandsNumberPerOccurrence()
    {
        var series = BuildRecurringSeries(number: 100L, totalInstallments: 3);
        SetupRepositoryReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().HaveCount(3);
        occurrences[0].Number.Should().Be(100L);
        occurrences[1].Number.Should().Be(101L);
        occurrences[2].Number.Should().Be(102L);
    }

    [Fact]
    public async Task GetCalendar_RecurringInvoiceWithoutNumber_ReturnsNullNumbers()
    {
        var series = BuildRecurringSeries(number: null, totalInstallments: 3);
        SetupRepositoryReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().HaveCount(3);
        occurrences.Should().OnlyContain(o => o.Number == null);
    }

    [Fact]
    public async Task GetCalendar_OccurrenceOverrideWithExplicitNumber_UsesOverride()
    {
        var series = BuildRecurringSeries(number: 100L, totalInstallments: 3);
        var exception = BuildException(series, occurrenceDate: new DateOnly(2026, 2, 15), number: 9999L);
        SetupRepositoryReturns(series, exception);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().HaveCount(3);
        occurrences[0].Number.Should().Be(100L);
        occurrences[1].Number.Should().Be(9999L);
        occurrences[1].IsOverride.Should().BeTrue();
        occurrences[2].Number.Should().Be(102L);
    }

    [Fact]
    public async Task GetCalendar_OccurrenceOverrideWithoutNumber_FallsBackToComputed()
    {
        var series = BuildRecurringSeries(number: 100L, totalInstallments: 3);
        var exception = BuildException(series, occurrenceDate: new DateOnly(2026, 2, 15), number: null);
        SetupRepositoryReturns(series, exception);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences[1].IsOverride.Should().BeTrue();
        occurrences[1].Number.Should().Be(101L);
    }

    [Fact]
    public async Task GetCalendar_ClassAndPointOfSaleConstantAcrossOccurrences()
    {
        var series = BuildRecurringSeries(number: 100L, totalInstallments: 3);
        series.Class = InvoiceClass.A;
        series.PointOfSale = 1;
        SetupRepositoryReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().OnlyContain(o => o.Class == InvoiceClass.A && o.PointOfSale == 1);
    }

    [Fact]
    public async Task GetCalendar_CreditNoteOneOff_SubtractsFromMonthTotal()
    {
        var invoice = BuildOneOff(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 3, 10));
        var creditNote = BuildOneOff(type: InvoiceType.CreditNote, amount: 300m, date: new DateOnly(2026, 3, 20), parentInvoiceId: invoice.Id);
        SetupRepositoryReturns(invoice, creditNote);

        var result = await _sut.GetCalendar(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        result.IsError.Should().BeFalse();
        var monthIndex = result.Value.Months.IndexOf("2026-03");
        monthIndex.Should().BeGreaterOrEqualTo(0);
        result.Value.Totals[monthIndex]["USD"].Should().Be(700m);
    }

    [Fact]
    public async Task GetCalendar_DebitNoteOneOff_AddsToMonthTotal()
    {
        var invoice = BuildOneOff(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 3, 10));
        var debitNote = BuildOneOff(type: InvoiceType.DebitNote, amount: 150m, date: new DateOnly(2026, 3, 25), parentInvoiceId: invoice.Id);
        SetupRepositoryReturns(invoice, debitNote);

        var result = await _sut.GetCalendar(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        result.IsError.Should().BeFalse();
        var monthIndex = result.Value.Months.IndexOf("2026-03");
        result.Value.Totals[monthIndex]["USD"].Should().Be(1150m);
    }

    [Fact]
    public async Task GetCalendar_RecurringCreditNote_AllOccurrencesSubtract()
    {
        var parentInvoice = BuildOneOff(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 1, 1));
        var creditSeries = BuildRecurringSeries(number: null, totalInstallments: 3);
        creditSeries.Type = InvoiceType.CreditNote;
        creditSeries.ParentInvoiceId = parentInvoice.Id;
        SetupRepositoryReturns(parentInvoice, creditSeries);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var janIndex = result.Value.Months.IndexOf("2026-01");
        var febIndex = result.Value.Months.IndexOf("2026-02");
        var marIndex = result.Value.Months.IndexOf("2026-03");
        result.Value.Totals[janIndex]["USD"].Should().Be(900m);
        result.Value.Totals[febIndex]["USD"].Should().Be(-100m);
        result.Value.Totals[marIndex]["USD"].Should().Be(-100m);
    }

    [Fact]
    public async Task Create_CreditNoteWithoutParent_ReturnsParentRequired()
    {
        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentInvoiceId: null);

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentRequired);
    }

    [Fact]
    public async Task Create_InvoiceWithParent_ReturnsParentNotAllowed()
    {
        var request = BuildCreateRequest(type: InvoiceType.Invoice, parentInvoiceId: Guid.NewGuid());

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentNotAllowed);
    }

    [Fact]
    public async Task Create_CreditNoteWithNonexistentParent_ReturnsParentNotFound()
    {
        var parentId = Guid.NewGuid();
        _invoiceRepositoryMock
            .Setup(r => r.GetById(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentInvoiceId: parentId);

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentNotFound);
    }

    [Fact]
    public async Task Create_CreditNoteWithCreditNoteParent_ReturnsParentMustBeInvoice()
    {
        var parent = BuildOneOff(type: InvoiceType.CreditNote, amount: 100m, date: new DateOnly(2026, 1, 1));
        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentInvoiceId: parent.Id);

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentMustBeInvoice);
    }

    [Fact]
    public async Task Create_CreditNoteWithValidInvoiceParent_Succeeds()
    {
        var parent = BuildOneOff(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 1, 1));
        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentInvoiceId: parent.Id);

        var result = await _sut.Create(request);

        result.IsError.Should().BeFalse();
        result.Value.Type.Should().Be(InvoiceType.CreditNote);
        result.Value.ParentInvoiceId.Should().Be(parent.Id);
    }

    private static Invoice BuildOneOff(InvoiceType type, decimal amount, DateOnly date, Guid? parentInvoiceId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Date = date,
            Amount = amount,
            Currency = "USD",
            Description = type.ToString(),
            Type = type,
            ParentInvoiceId = parentInvoiceId
        };

    private static CreateInvoiceRequest BuildCreateRequest(InvoiceType type, Guid? parentInvoiceId) =>
        new(
            Date: new DateOnly(2026, 3, 10),
            Amount: 100m,
            Currency: "USD",
            Description: "Test",
            Source: null,
            Type: type,
            ParentInvoiceId: parentInvoiceId,
            Class: null,
            PointOfSale: null,
            Number: null,
            Recurrence: null);

    private static Invoice BuildRecurringSeries(long? number, int totalInstallments) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Date = new DateOnly(2026, 1, 15),
            Amount = 100m,
            Currency = "USD",
            Description = "Rent",
            Number = number,
            RecurrenceRule = new RecurrenceRule
            {
                Frequency = RecurrenceFrequency.Monthly,
                Interval = 1,
                EndDate = null,
                TotalInstallments = totalInstallments
            }
        };

    private static Invoice BuildException(Invoice series, DateOnly occurrenceDate, long? number) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Date = occurrenceDate,
            Amount = series.Amount,
            Currency = series.Currency,
            Description = series.Description,
            Number = number,
            RecurringInvoiceId = series.Id,
            OriginalDate = occurrenceDate
        };

    private void SetupRepositoryReturns(params Invoice[] invoices)
    {
        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);
    }

    private static List<Application.DTOs.Invoice.InvoiceResponse> GetOrderedOccurrences(
        Application.DTOs.Shared.CalendarResponse<Application.DTOs.Invoice.InvoiceCalendarRow> response) =>
        response.Rows
            .SelectMany(r => r.Occurrences.Values.SelectMany(v => v))
            .OrderBy(o => o.Date)
            .ToList();
}
