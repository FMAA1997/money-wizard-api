using Application.Services;
using Domain.Abstractions.Clients;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class ArgentinaServiceTests
{
    private readonly Mock<IDolarApiClient> _dolarApiClientMock = new();
    private readonly Mock<IArgentinaDatosClient> _argentinaDatosClientMock = new();

    private ArgentinaService Build() => new(_dolarApiClientMock.Object, _argentinaDatosClientMock.Object);

    [Fact]
    public async Task GetDollarsAsync_HappyPath_MapsAllFields()
    {
        var fechaActualizacion = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        _dolarApiClientMock
            .Setup(c => c.GetDollarRatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DollarRateEntry>
            {
                new("oficial", "Oficial", "USD", 1360m, 1410m, fechaActualizacion, null),
                new("mayorista", "Mayorista", "USD", 1379m, 1388m, fechaActualizacion, -0.36m),
            });

        var result = await Build().GetDollarsAsync();

        result.IsError.Should().BeFalse();
        result.Value.Rates.Should().HaveCount(2);
        result.Value.Rates[0].Casa.Should().Be("oficial");
        result.Value.Rates[0].Compra.Should().Be(1360m);
        result.Value.Rates[0].Venta.Should().Be(1410m);
        result.Value.Rates[0].Variacion.Should().BeNull();
        result.Value.Rates[1].Variacion.Should().Be(-0.36m);
        result.Value.Rates[1].FechaActualizacion.Should().Be(fechaActualizacion);
    }

    [Fact]
    public async Task GetMacroAsync_HappyPath_ComputesRiesgoVariationAndExposesPreviousInflation()
    {
        SetupRiesgoSeries(
            (new DateOnly(2026, 5, 5), 800m),
            (new DateOnly(2026, 5, 6), 820m),
            (new DateOnly(2026, 5, 7), 808m));

        SetupMomSeries(
            (new DateOnly(2026, 3, 31), 3.5m),
            (new DateOnly(2026, 4, 30), 2.8m));

        SetupYoySeries(
            (new DateOnly(2026, 3, 31), 220m),
            (new DateOnly(2026, 4, 30), 195m));

        var result = await Build().GetMacroAsync();

        result.IsError.Should().BeFalse();
        result.Value.RiesgoPais.Valor.Should().Be(808m);
        result.Value.RiesgoPais.Fecha.Should().Be(new DateOnly(2026, 5, 7));
        result.Value.RiesgoPais.AbsoluteChangeBps.Should().Be(-12m);
        result.Value.RiesgoPais.PercentChange.Should().Be(Math.Round(-12m / 820m * 100m, 2));
        result.Value.RiesgoPais.PreviousFecha.Should().Be(new DateOnly(2026, 5, 6));

        result.Value.Mom.Latest.Should().Be(2.8m);
        result.Value.Mom.LatestFecha.Should().Be(new DateOnly(2026, 4, 30));
        result.Value.Mom.Previous.Should().Be(3.5m);

        result.Value.Yoy.Latest.Should().Be(195m);
        result.Value.Yoy.Previous.Should().Be(220m);
    }

    [Fact]
    public async Task GetMacroAsync_SingleEntrySeries_NullsOutChangeAndPrevious()
    {
        SetupRiesgoSeries((new DateOnly(2026, 5, 7), 808m));
        SetupMomSeries((new DateOnly(2026, 4, 30), 2.8m));
        SetupYoySeries((new DateOnly(2026, 4, 30), 195m));

        var result = await Build().GetMacroAsync();

        result.IsError.Should().BeFalse();
        result.Value.RiesgoPais.AbsoluteChangeBps.Should().BeNull();
        result.Value.RiesgoPais.PercentChange.Should().BeNull();
        result.Value.RiesgoPais.PreviousFecha.Should().BeNull();
        result.Value.Mom.Previous.Should().BeNull();
        result.Value.Yoy.Previous.Should().BeNull();
    }

    [Fact]
    public async Task GetMacroAsync_EmptySeries_ReturnsFailure()
    {
        SetupRiesgoSeries();
        SetupMomSeries((new DateOnly(2026, 4, 30), 2.8m));
        SetupYoySeries((new DateOnly(2026, 4, 30), 195m));

        var result = await Build().GetMacroAsync();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ArgentinaMacro.NoData");
    }

    [Fact]
    public async Task GetMacroAsync_PreviousValueIsZero_PercentChangeIsNullButAbsoluteIsComputed()
    {
        SetupRiesgoSeries(
            (new DateOnly(2026, 5, 6), 0m),
            (new DateOnly(2026, 5, 7), 50m));
        SetupMomSeries((new DateOnly(2026, 4, 30), 2.8m));
        SetupYoySeries((new DateOnly(2026, 4, 30), 195m));

        var result = await Build().GetMacroAsync();

        result.IsError.Should().BeFalse();
        result.Value.RiesgoPais.AbsoluteChangeBps.Should().Be(50m);
        result.Value.RiesgoPais.PercentChange.Should().BeNull();
    }

    [Fact]
    public async Task GetUpcomingHolidaysAsync_NLessThanOrEqualZero_ReturnsValidationError()
    {
        var result = await Build().GetUpcomingHolidaysAsync(0);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ArgentinaHolidays.InvalidCount");
    }

    [Fact]
    public async Task GetUpcomingHolidaysAsync_NTooLarge_ReturnsValidationError()
    {
        var result = await Build().GetUpcomingHolidaysAsync(51);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ArgentinaHolidays.InvalidCount");
    }

    [Fact]
    public async Task GetUpcomingHolidaysAsync_CurrentYearHasEnough_DoesNotFetchNextYear()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = today.Year;
        var holidays = new List<HolidayEntry>
        {
            new(today.AddDays(1), "inamovible", "A"),
            new(today.AddDays(5), "trasladable", "B"),
            new(today.AddDays(10), "puente", "C"),
            new(today.AddDays(20), "inamovible", "D"),
        };

        _argentinaDatosClientMock
            .Setup(c => c.GetHolidaysAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holidays);

        var result = await Build().GetUpcomingHolidaysAsync(3);

        result.IsError.Should().BeFalse();
        result.Value.Holidays.Should().HaveCount(3);
        result.Value.Holidays.Select(h => h.Nombre).Should().ContainInOrder("A", "B", "C");

        _argentinaDatosClientMock.Verify(c => c.GetHolidaysAsync(year, It.IsAny<CancellationToken>()), Times.Once);
        _argentinaDatosClientMock.Verify(c => c.GetHolidaysAsync(year + 1, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUpcomingHolidaysAsync_CurrentYearShort_SpillsIntoNextYear()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = today.Year;

        _argentinaDatosClientMock
            .Setup(c => c.GetHolidaysAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HolidayEntry>
            {
                new(today.AddDays(1), "inamovible", "A"),
            });

        _argentinaDatosClientMock
            .Setup(c => c.GetHolidaysAsync(year + 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HolidayEntry>
            {
                new(new DateOnly(year + 1, 1, 1), "inamovible", "Año nuevo"),
                new(new DateOnly(year + 1, 2, 16), "inamovible", "Carnaval"),
                new(new DateOnly(year + 1, 5, 1), "inamovible", "Día del Trabajador"),
            });

        var result = await Build().GetUpcomingHolidaysAsync(3);

        result.IsError.Should().BeFalse();
        result.Value.Holidays.Should().HaveCount(3);
        result.Value.Holidays[0].Nombre.Should().Be("A");
        result.Value.Holidays[1].Nombre.Should().Be("Año nuevo");
        result.Value.Holidays[2].Nombre.Should().Be("Carnaval");
    }

    [Fact]
    public async Task GetUpcomingHolidaysAsync_FiltersPastHolidaysOfCurrentYear()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = today.Year;

        _argentinaDatosClientMock
            .Setup(c => c.GetHolidaysAsync(year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HolidayEntry>
            {
                new(today.AddDays(-30), "inamovible", "Past"),
                new(today.AddDays(2), "inamovible", "Future"),
            });
        _argentinaDatosClientMock
            .Setup(c => c.GetHolidaysAsync(year + 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HolidayEntry>());

        var result = await Build().GetUpcomingHolidaysAsync(5);

        result.IsError.Should().BeFalse();
        result.Value.Holidays.Should().ContainSingle(h => h.Nombre == "Future");
        result.Value.Holidays.Should().NotContain(h => h.Nombre == "Past");
    }

    private void SetupRiesgoSeries(params (DateOnly Fecha, decimal Valor)[] points) =>
        _argentinaDatosClientMock
            .Setup(c => c.GetRiesgoPaisHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(points.Select(p => new IndicatorPoint(p.Fecha, p.Valor)).ToList());

    private void SetupMomSeries(params (DateOnly Fecha, decimal Valor)[] points) =>
        _argentinaDatosClientMock
            .Setup(c => c.GetInflationHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(points.Select(p => new IndicatorPoint(p.Fecha, p.Valor)).ToList());

    private void SetupYoySeries(params (DateOnly Fecha, decimal Valor)[] points) =>
        _argentinaDatosClientMock
            .Setup(c => c.GetInflationYoYHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(points.Select(p => new IndicatorPoint(p.Fecha, p.Valor)).ToList());
}
