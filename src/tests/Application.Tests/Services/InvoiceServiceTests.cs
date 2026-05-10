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
        var series = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 3);
        SetupRangeReturns(series);

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
        var series = BuildRecurringSeries(baseNumber: null, totalInstallments: 3);
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().HaveCount(3);
        occurrences.Should().OnlyContain(o => o.Number == null);
    }

    [Fact]
    public async Task GetCalendar_OccurrenceOverrideWithFrozenNumber_UsesFrozen()
    {
        var series = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 3);
        AddException(series, originalDate: new DateOnly(2026, 2, 15), number: 9999L);
        SetupRangeReturns(series);

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
        var series = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 3);
        AddException(series, originalDate: new DateOnly(2026, 2, 15), number: null);
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences[1].IsOverride.Should().BeTrue();
        occurrences[1].Number.Should().Be(101L);
    }

    [Fact]
    public async Task GetCalendar_ClassAndPointOfSaleConstantAcrossOccurrences()
    {
        var series = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 3);
        series.Class = InvoiceClass.A;
        series.PointOfSale = 1;
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = GetOrderedOccurrences(result.Value);
        occurrences.Should().OnlyContain(o => o.Class == InvoiceClass.A && o.PointOfSale == 1);
    }

    [Fact]
    public async Task GetCalendar_CreditNoteOneOff_SubtractsFromMonthTotal()
    {
        var invoice = BuildOneOffSeries(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 3, 10));
        var creditNote = BuildOneOffSeries(type: InvoiceType.CreditNote, amount: 300m, date: new DateOnly(2026, 3, 20));
        SetupRangeReturns(invoice, creditNote);

        var result = await _sut.GetCalendar(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        result.IsError.Should().BeFalse();
        var monthIndex = result.Value.Months.IndexOf("2026-03");
        monthIndex.Should().BeGreaterThanOrEqualTo(0);
        result.Value.Totals[monthIndex]["USD"].Should().Be(700m);
    }

    [Fact]
    public async Task GetCalendar_DebitNoteOneOff_AddsToMonthTotal()
    {
        var invoice = BuildOneOffSeries(type: InvoiceType.Invoice, amount: 1000m, date: new DateOnly(2026, 3, 10));
        var debitNote = BuildOneOffSeries(type: InvoiceType.DebitNote, amount: 150m, date: new DateOnly(2026, 3, 25));
        SetupRangeReturns(invoice, debitNote);

        var result = await _sut.GetCalendar(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        result.IsError.Should().BeFalse();
        var monthIndex = result.Value.Months.IndexOf("2026-03");
        result.Value.Totals[monthIndex]["USD"].Should().Be(1150m);
    }

    [Fact]
    public async Task Create_CreditNoteWithoutParent_ReturnsParentRequired()
    {
        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentSeriesId: null, parentOriginalDate: null);

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentRequired);
    }

    [Fact]
    public async Task Create_InvoiceWithParent_ReturnsParentNotAllowed()
    {
        var request = BuildCreateRequest(type: InvoiceType.Invoice, parentSeriesId: Guid.NewGuid(), parentOriginalDate: new DateOnly(2026, 1, 1));

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
            .ReturnsAsync((InvoiceSeries?)null);

        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentSeriesId: parentId, parentOriginalDate: new DateOnly(2026, 1, 1));

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentNotFound);
    }

    [Fact]
    public async Task Create_CreditNoteWithCreditNoteParent_ReturnsParentMustBeInvoice()
    {
        var parent = BuildOneOffSeries(type: InvoiceType.CreditNote, amount: 100m, date: new DateOnly(2026, 1, 1));
        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var request = BuildCreateRequest(type: InvoiceType.CreditNote, parentSeriesId: parent.Id, parentOriginalDate: new DateOnly(2026, 1, 1));

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentMustBeInvoice);
    }

    [Fact]
    public async Task Create_CreditNoteWithValidInvoiceParent_MaterializesParentExceptionAndSucceeds()
    {
        var parent = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 12);
        parent.Type = InvoiceType.Invoice;
        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _invoiceRepositoryMock
            .Setup(r => r.GetException(parent.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceException?)null);

        InvoiceException? capturedException = null;
        _invoiceRepositoryMock
            .Setup(r => r.AddException(It.IsAny<InvoiceException>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceException, CancellationToken>((e, _) =>
            {
                capturedException = e;
                parent.Exceptions.Add(e);
            });

        InvoiceSeries? capturedSeries = null;
        _invoiceRepositoryMock
            .Setup(r => r.Add(It.IsAny<InvoiceSeries>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceSeries, CancellationToken>((s, _) => capturedSeries = s);

        var request = BuildCreateRequest(
            type: InvoiceType.CreditNote,
            parentSeriesId: parent.Id,
            parentOriginalDate: new DateOnly(2026, 6, 15));

        var result = await _sut.Create(request);

        result.IsError.Should().BeFalse();
        capturedException.Should().NotBeNull();
        capturedException!.OriginalDate.Should().Be(new DateOnly(2026, 6, 15));
        capturedException.Number.Should().Be(105L); // BaseNumber 100 + globalIndex 5 (Jan..Jun -> indexes 0..5)
        capturedException.IsDeleted.Should().BeFalse();

        capturedSeries.Should().NotBeNull();
        capturedSeries!.Type.Should().Be(InvoiceType.CreditNote);
        capturedSeries.ParentExceptionId.Should().Be(capturedException.Id);
    }

    [Fact]
    public async Task Create_CreditNoteAgainstAlreadyDeletedParentOccurrence_ReturnsError()
    {
        var parent = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 12);
        parent.Type = InvoiceType.Invoice;
        var existingException = new InvoiceException
        {
            Id = Guid.NewGuid(),
            SeriesId = parent.Id,
            OriginalDate = new DateOnly(2026, 6, 15),
            IsDeleted = true,
        };
        parent.Exceptions.Add(existingException);

        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _invoiceRepositoryMock
            .Setup(r => r.GetException(parent.Id, new DateOnly(2026, 6, 15), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingException);

        var request = BuildCreateRequest(
            type: InvoiceType.CreditNote,
            parentSeriesId: parent.Id,
            parentOriginalDate: new DateOnly(2026, 6, 15));

        var result = await _sut.Create(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(InvoiceErrors.ParentOccurrenceNotValid);
    }

    [Fact]
    public async Task Create_CreditNoteAgainstExistingException_ReusesIt()
    {
        var parent = BuildRecurringSeries(baseNumber: 100L, totalInstallments: 12);
        parent.Type = InvoiceType.Invoice;
        var existingException = new InvoiceException
        {
            Id = Guid.NewGuid(),
            SeriesId = parent.Id,
            OriginalDate = new DateOnly(2026, 6, 15),
            Number = 105L,
            IsDeleted = false,
        };
        parent.Exceptions.Add(existingException);

        _invoiceRepositoryMock
            .Setup(r => r.GetById(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _invoiceRepositoryMock
            .Setup(r => r.GetException(parent.Id, new DateOnly(2026, 6, 15), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingException);

        var addExceptionCalled = false;
        _invoiceRepositoryMock
            .Setup(r => r.AddException(It.IsAny<InvoiceException>(), It.IsAny<CancellationToken>()))
            .Callback(() => addExceptionCalled = true);

        InvoiceSeries? capturedSeries = null;
        _invoiceRepositoryMock
            .Setup(r => r.Add(It.IsAny<InvoiceSeries>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceSeries, CancellationToken>((s, _) => capturedSeries = s);

        var request = BuildCreateRequest(
            type: InvoiceType.CreditNote,
            parentSeriesId: parent.Id,
            parentOriginalDate: new DateOnly(2026, 6, 15));

        var result = await _sut.Create(request);

        result.IsError.Should().BeFalse();
        addExceptionCalled.Should().BeFalse();
        capturedSeries!.ParentExceptionId.Should().Be(existingException.Id);
    }

    private static InvoiceSeries BuildRecurringSeries(long? baseNumber, int totalInstallments)
    {
        var series = new InvoiceSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Rent",
            Type = InvoiceType.Invoice,
            BaseNumber = baseNumber,
        };
        series.Segments.Add(new InvoiceSegment
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            EffectiveFrom = new DateOnly(2026, 1, 15),
            Amount = 100m,
            Currency = "USD",
            RecurrenceRule = new RecurrenceRule
            {
                Frequency = RecurrenceFrequency.Monthly,
                Interval = 1,
                EndDate = null,
                TotalInstallments = totalInstallments,
            },
        });
        return series;
    }

    private static InvoiceSeries BuildOneOffSeries(InvoiceType type, decimal amount, DateOnly date)
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

    private static void AddException(InvoiceSeries series, DateOnly originalDate, long? number)
    {
        series.Exceptions.Add(new InvoiceException
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            OriginalDate = originalDate,
            Date = originalDate,
            Number = number,
            IsDeleted = false,
        });
    }

    private static CreateInvoiceRequest BuildCreateRequest(InvoiceType type, Guid? parentSeriesId, DateOnly? parentOriginalDate) =>
        new(
            Date: new DateOnly(2026, 3, 10),
            Amount: 100m,
            Currency: "USD",
            Description: "Test",
            Source: null,
            Type: type,
            ParentInvoiceSeriesId: parentSeriesId,
            ParentOriginalDate: parentOriginalDate,
            Class: null,
            PointOfSale: null,
            BaseNumber: null,
            Recurrence: null);

    private void SetupRangeReturns(params InvoiceSeries[] seriesList)
    {
        _invoiceRepositoryMock
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seriesList);
    }

    private static List<Application.DTOs.Invoice.InvoiceResponse> GetOrderedOccurrences(
        Application.DTOs.Shared.CalendarResponse<Application.DTOs.Invoice.InvoiceCalendarRow> response) =>
        response.Rows
            .SelectMany(r => r.Occurrences.Values.SelectMany(v => v))
            .OrderBy(o => o.Date)
            .ToList();
}
