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

public class PaycheckServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IPaycheckRepository> _paycheckRepositoryMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly PaycheckService _sut;

    public PaycheckServiceTests()
    {
        _currentUserProviderMock.Setup(p => p.UserId).Returns(UserId);

        _sut = new PaycheckService(
            _paycheckRepositoryMock.Object,
            _currentUserProviderMock.Object,
            _unitOfWorkMock.Object,
            TestCurrencyConverter.Create());
    }

    [Fact]
    public async Task Create_WithRecurrence_AddsSeriesWithSegment()
    {
        PaycheckSeries? captured = null;
        _paycheckRepositoryMock
            .Setup(r => r.Add(It.IsAny<PaycheckSeries>(), It.IsAny<CancellationToken>()))
            .Callback<PaycheckSeries, CancellationToken>((s, _) => captured = s);

        var request = new CreatePaycheckRequest(
            new DateOnly(2026, 1, 15), 1000m, "USD", "Salary",
            new CreateRecurrenceRequest(RecurrenceFrequency.Monthly, 1, null, null));

        var result = await _sut.Create(request);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Description.Should().Be("Salary");
        captured.Segments.Should().HaveCount(1);
        var segment = captured.Segments.Single();
        segment.EffectiveFrom.Should().Be(new DateOnly(2026, 1, 15));
        segment.Amount.Should().Be(1000m);
        segment.RecurrenceRule.Should().NotBeNull();
        segment.RecurrenceRule!.Frequency.Should().Be(RecurrenceFrequency.Monthly);
    }

    [Fact]
    public async Task GetCalendar_TwoSegmentsAcrossYear_ExpandsBothAtTheirOwnAmounts()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true),
            BuildSegment(new DateOnly(2026, 7, 15), amount: 1100m, monthlyForever: true));
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var rows = result.Value.Rows;
        rows.Should().HaveCount(1);
        var row = rows.Single();
        row.Description.Should().Be("Salary");
        row.IsRecurring.Should().BeTrue();

        var allOccurrences = row.Occurrences.SelectMany(kv => kv.Value).OrderBy(o => o.Date).ToList();
        allOccurrences.Should().HaveCount(12);

        // 6 occurrences at 1000 (Jan-Jun), 6 at 1100 (Jul-Dec)
        allOccurrences.Take(6).Should().OnlyContain(o => o.Amount == 1000m);
        allOccurrences.Skip(6).Should().OnlyContain(o => o.Amount == 1100m);
    }

    [Fact]
    public async Task UpdateFromDate_AppendsSegmentAndCapsPrevious()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        SetupGetById(series);

        PaycheckSegment? newSegment = null;
        _paycheckRepositoryMock
            .Setup(r => r.AddSegment(It.IsAny<PaycheckSegment>(), It.IsAny<CancellationToken>()))
            .Callback<PaycheckSegment, CancellationToken>((s, _) => newSegment = s);

        var request = new UpdatePaycheckFromDateRequest(
            1100m, "USD", new CreateRecurrenceRequest(RecurrenceFrequency.Monthly, 1, null, null));

        var result = await _sut.UpdateFromDate(series.Id, new DateOnly(2026, 7, 15), request);

        result.IsError.Should().BeFalse();
        newSegment.Should().NotBeNull();
        newSegment!.EffectiveFrom.Should().Be(new DateOnly(2026, 7, 15));
        newSegment.Amount.Should().Be(1100m);

        // Original segment's recurrence rule should be capped at the previous occurrence (June 15).
        var originalSegment = series.Segments.Single(s => s.EffectiveFrom == new DateOnly(2026, 1, 15));
        originalSegment.RecurrenceRule!.EndDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public async Task UpdateOccurrence_CreatesException()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        SetupGetById(series);
        _paycheckRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaycheckException?)null);

        PaycheckException? captured = null;
        _paycheckRepositoryMock
            .Setup(r => r.AddException(It.IsAny<PaycheckException>(), It.IsAny<CancellationToken>()))
            .Callback<PaycheckException, CancellationToken>((e, _) => captured = e);

        var request = new UpdatePaycheckOccurrenceRequest(null, 1500m, "USD");
        var result = await _sut.UpdateOccurrence(series.Id, new DateOnly(2026, 3, 15), request);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.OriginalDate.Should().Be(new DateOnly(2026, 3, 15));
        captured.Amount.Should().Be(1500m);
        captured.IsDeleted.Should().BeFalse();

        result.Value.Amount.Should().Be(1500m);
        result.Value.IsOverride.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOccurrence_CreatesDeletedException()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        SetupGetById(series);
        _paycheckRepositoryMock
            .Setup(r => r.GetException(series.Id, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaycheckException?)null);

        PaycheckException? captured = null;
        _paycheckRepositoryMock
            .Setup(r => r.AddException(It.IsAny<PaycheckException>(), It.IsAny<CancellationToken>()))
            .Callback<PaycheckException, CancellationToken>((e, _) => captured = e);

        var result = await _sut.DeleteOccurrence(series.Id, new DateOnly(2026, 3, 15));

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.IsDeleted.Should().BeTrue();
        captured.OriginalDate.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public async Task UpdateOccurrence_InvalidDate_ReturnsError()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        SetupGetById(series);

        var request = new UpdatePaycheckOccurrenceRequest(null, 1500m, "USD");
        var result = await _sut.UpdateOccurrence(series.Id, new DateOnly(2026, 3, 16), request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(RecurrenceErrors.InvalidOccurrenceDate);
    }

    [Fact]
    public async Task Update_ChangesDescription_OnlyUpdatesSeries()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        SetupGetById(series);

        var result = await _sut.Update(series.Id, new UpdatePaycheckRequest("Renamed Salary"));

        result.IsError.Should().BeFalse();
        series.Description.Should().Be("Renamed Salary");
        series.Segments.Single().Amount.Should().Be(1000m); // segment unchanged
    }

    [Fact]
    public async Task GetCalendar_OverrideOnSegment_UsesOverrideAmount()
    {
        var series = BuildSeries(
            BuildSegment(new DateOnly(2026, 1, 15), amount: 1000m, monthlyForever: true));
        series.Exceptions.Add(new PaycheckException
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            OriginalDate = new DateOnly(2026, 4, 15),
            Date = new DateOnly(2026, 4, 15),
            Amount = 1500m,
            Currency = "USD",
        });
        SetupRangeReturns(series);

        var result = await _sut.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        result.IsError.Should().BeFalse();
        var occurrences = result.Value.Rows.Single().Occurrences
            .SelectMany(kv => kv.Value).OrderBy(o => o.Date).ToList();

        var april = occurrences.Single(o => o.Date == new DateOnly(2026, 4, 15));
        april.Amount.Should().Be(1500m);
        april.IsOverride.Should().BeTrue();
        occurrences.Where(o => o.Date != new DateOnly(2026, 4, 15))
            .Should().OnlyContain(o => o.Amount == 1000m);
    }

    private void SetupGetById(PaycheckSeries series) =>
        _paycheckRepositoryMock
            .Setup(r => r.GetById(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

    private void SetupRangeReturns(params PaycheckSeries[] seriesList) =>
        _paycheckRepositoryMock
            .Setup(r => r.GetByUserIdInRange(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seriesList);

    private static PaycheckSeries BuildSeries(params PaycheckSegment[] segments)
    {
        var series = new PaycheckSeries
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Salary",
        };
        foreach (var segment in segments)
        {
            segment.SeriesId = series.Id;
            series.Segments.Add(segment);
        }
        return series;
    }

    private static PaycheckSegment BuildSegment(DateOnly effectiveFrom, decimal amount, bool monthlyForever) =>
        new()
        {
            Id = Guid.NewGuid(),
            EffectiveFrom = effectiveFrom,
            Amount = amount,
            Currency = "USD",
            RecurrenceRule = monthlyForever
                ? new RecurrenceRule { Frequency = RecurrenceFrequency.Monthly, Interval = 1 }
                : null,
        };
}
