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

public class ExpenseServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IExpenseRepository> _expenseRepositoryMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _currentUserProviderMock.Setup(p => p.UserId).Returns(UserId);

        _sut = new ExpenseService(
            _expenseRepositoryMock.Object,
            _currentUserProviderMock.Object,
            _unitOfWorkMock.Object,
            TestCurrencyConverter.Create());
    }

    [Fact]
    public async Task Create_WithRecurrence_AddsSeriesWithSegment()
    {
        ExpenseSeries? captured = null;
        _expenseRepositoryMock
            .Setup(r => r.Add(It.IsAny<ExpenseSeries>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseSeries, CancellationToken>((s, _) => captured = s);

        var paycheckSeriesId = Guid.NewGuid();
        var request = new CreateExpenseRequest(
            new DateOnly(2026, 1, 15), 500m, "USD", "Rent",
            CategoryId: null,
            PaycheckSeriesId: paycheckSeriesId,
            InvoiceSeriesId: null,
            Recurrence: new CreateRecurrenceRequest(RecurrenceFrequency.Monthly, 1, null, null));

        var result = await _sut.Create(request);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Description.Should().Be("Rent");
        captured.Segments.Should().HaveCount(1);
        var segment = captured.Segments.Single();
        segment.EffectiveFrom.Should().Be(new DateOnly(2026, 1, 15));
        segment.Amount.Should().Be(500m);
        segment.PaycheckSeriesId.Should().Be(paycheckSeriesId);
        segment.RecurrenceRule.Should().NotBeNull();
        segment.RecurrenceRule!.Frequency.Should().Be(RecurrenceFrequency.Monthly);
    }

    [Fact]
    public async Task GetCalendar_TwoSegmentsWithDifferentSources_ExposesActiveSegmentSource()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var paycheckA = new PaycheckSeries { Id = sourceA, UserId = UserId, Description = "Job A" };
        var paycheckB = new PaycheckSeries { Id = sourceB, UserId = UserId, Description = "Job B" };

        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true, paycheckSeries: paycheckA),
            BuildSegment(new DateOnly(2026, 7, 15), amount: 600m, monthlyForever: true, paycheckSeries: paycheckB));
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var rows = result.Value.Rows;
        rows.Should().HaveCount(1);
        var row = rows.Single();
        row.Description.Should().Be("Rent");

        var allOccurrences = row.Occurrences.SelectMany(kv => kv.Value).OrderBy(o => o.Date).ToList();
        allOccurrences.Should().HaveCount(12);
        allOccurrences.Take(6).Should().OnlyContain(o => o.Amount == 500m && o.PaycheckSeriesId == sourceA);
        allOccurrences.Skip(6).Should().OnlyContain(o => o.Amount == 600m && o.PaycheckSeriesId == sourceB);
    }

    [Fact]
    public async Task UpdateFromDate_AppendsSegmentAndCapsPrevious()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);

        ExpenseSegment? newSegment = null;
        _expenseRepositoryMock
            .Setup(r => r.AddSegment(It.IsAny<ExpenseSegment>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseSegment, CancellationToken>((s, _) => newSegment = s);

        var request = new UpdateExpenseFromDateRequest(
            600m, "USD", PaycheckSeriesId: null, InvoiceSeriesId: null,
            new CreateRecurrenceRequest(RecurrenceFrequency.Monthly, 1, null, null));

        var result = await _sut.UpdateFromDate(series.Id, new DateOnly(2026, 7, 15), request);

        result.IsError.Should().BeFalse();
        newSegment.Should().NotBeNull();
        newSegment!.EffectiveFrom.Should().Be(new DateOnly(2026, 7, 15));
        newSegment.Amount.Should().Be(600m);

        var originalSegment = series.Segments.Single(s => s.EffectiveFrom == new DateOnly(2026, 1, 15));
        originalSegment.RecurrenceRule!.EndDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public async Task UpdateOccurrence_CreatesException()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);
        _expenseRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseException?)null);

        ExpenseException? captured = null;
        _expenseRepositoryMock
            .Setup(r => r.AddException(It.IsAny<ExpenseException>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseException, CancellationToken>((e, _) => captured = e);

        var request = new UpdateExpenseOccurrenceRequest(null, 750m, "USD");
        var result = await _sut.UpdateOccurrence(series.Id, new DateOnly(2026, 3, 15), request);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.OriginalDate.Should().Be(new DateOnly(2026, 3, 15));
        captured.Amount.Should().Be(750m);
        captured.IsDeleted.Should().BeFalse();

        result.Value.Amount.Should().Be(750m);
        result.Value.IsOverride.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOccurrence_CreatesDeletedException()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);
        _expenseRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseException?)null);

        ExpenseException? captured = null;
        _expenseRepositoryMock
            .Setup(r => r.AddException(It.IsAny<ExpenseException>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseException, CancellationToken>((e, _) => captured = e);

        var result = await _sut.DeleteOccurrence(series.Id, new DateOnly(2026, 3, 15));

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.IsDeleted.Should().BeTrue();
        captured.OriginalDate.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public async Task UpdateOccurrence_NonRecurrenceDate_CreatesInsertion()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);
        _expenseRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseException?)null);

        ExpenseException? captured = null;
        _expenseRepositoryMock
            .Setup(r => r.AddException(It.IsAny<ExpenseException>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseException, CancellationToken>((e, _) => captured = e);

        var request = new UpdateExpenseOccurrenceRequest(null, 750m, "USD");
        var result = await _sut.UpdateOccurrence(series.Id, new DateOnly(2026, 3, 16), request);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.OriginalDate.Should().BeNull();
        captured.Date.Should().Be(new DateOnly(2026, 3, 16));
        captured.Amount.Should().Be(750m);
        captured.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateOccurrence_NonRecurrenceDateMissingAmount_ReturnsError()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);
        _expenseRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseException?)null);

        var request = new UpdateExpenseOccurrenceRequest(null, null, null);
        var result = await _sut.UpdateOccurrence(series.Id, new DateOnly(2026, 3, 16), request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(RecurrenceErrors.InsertionRequiresAmountAndCurrency);
    }

    [Fact]
    public async Task Update_ChangesDescriptionAndCategory_OnlyUpdatesSeries()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        SetupGetById(series);

        var newCategoryId = Guid.NewGuid();
        var result = await _sut.Update(series.Id, new UpdateExpenseRequest("Renamed Rent", newCategoryId));

        result.IsError.Should().BeFalse();
        series.Description.Should().Be("Renamed Rent");
        series.CategoryId.Should().Be(newCategoryId);
        series.Segments.Single().Amount.Should().Be(500m);
    }

    [Fact]
    public async Task GetCalendar_OverrideOnSegment_UsesOverrideAmount()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 500m, monthlyForever: true));
        series.Exceptions.Add(new ExpenseException
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            OriginalDate = new DateOnly(2026, 4, 15),
            Date = new DateOnly(2026, 4, 15),
            Amount = 800m,
            Currency = "USD",
        });
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = result.Value.Rows.Single().Occurrences
            .SelectMany(kv => kv.Value).OrderBy(o => o.Date).ToList();

        var april = occurrences.Single(o => o.Date == new DateOnly(2026, 4, 15));
        april.Amount.Should().Be(800m);
        april.IsOverride.Should().BeTrue();
        occurrences.Where(o => o.Date != new DateOnly(2026, 4, 15))
            .Should().OnlyContain(o => o.Amount == 500m);
    }

    private void SetupGetById(ExpenseSeries series) =>
        _expenseRepositoryMock
            .Setup(r => r.GetById(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

    private void SetupRangeReturns(params ExpenseSeries[] seriesList) =>
        _expenseRepositoryMock
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seriesList);

    private static ExpenseSeries BuildSeries(params ExpenseSegment[] segments)
    {
        var series = new ExpenseSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Rent",
        };
        foreach (var segment in segments)
        {
            segment.SeriesId = series.Id;
            series.Segments.Add(segment);
        }
        return series;
    }

    private static ExpenseSegment BuildSegment(
        DateOnly effectiveFrom,
        decimal amount,
        bool monthlyForever,
        PaycheckSeries? paycheckSeries = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            EffectiveFrom = effectiveFrom,
            Amount = amount,
            Currency = "USD",
            PaycheckSeriesId = paycheckSeries?.Id,
            PaycheckSeries = paycheckSeries,
            RecurrenceRule = monthlyForever
                ? new RecurrenceRule { Frequency = RecurrenceFrequency.Monthly, Interval = 1 }
                : null,
        };
}
