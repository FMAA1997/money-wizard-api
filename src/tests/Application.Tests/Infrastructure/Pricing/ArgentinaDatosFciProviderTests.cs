using Domain.Abstractions.Clients;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Clients;
using Infrastructure.Pricing;
using Infrastructure.Pricing.Providers;
using Moq;

namespace Application.Tests.Infrastructure.Pricing;

public class ArgentinaDatosFciProviderTests
{
    private readonly Mock<IArgentinaDatosClient> _client = new();

    private ArgentinaDatosFciProvider Build() =>
        new(new ArgentinaDatosFciSnapshotCache(_client.Object));

    [Fact]
    public void AssetClass_IsFci()
    {
        Build().AssetClass.Should().Be(AssetClass.Fci);
    }

    [Fact]
    public async Task GetCurrentPrice_HappyPath_ReturnsQuotedPrice()
    {
        var quote = MakeQuote("Galileo Renta Fija - Clase B", "Renta Fija", 1234.56m, "ARS", new DateOnly(2026, 5, 15));
        SetupRentaFija(quote);
        SetupOtherCategoriesEmpty();

        var result = await Build().GetCurrentPrice(quote.Ticker);

        result.IsError.Should().BeFalse();
        result.Value.Ticker.Should().Be(quote.Ticker);
        result.Value.Price.Should().Be(1234.56m);
        result.Value.Currency.Should().Be("ARS");
        result.Value.AsOf.Should().Be(new DateOnly(2026, 5, 15));
    }

    [Fact]
    public async Task GetCurrentPrice_EmptyTicker_ReturnsValidationError()
    {
        SetupAllCategoriesEmpty();

        var result = await Build().GetCurrentPrice("   ");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Pricing.TickerRequired");
    }

    [Fact]
    public async Task GetCurrentPrice_UnknownTicker_ReturnsNotFound()
    {
        SetupRentaFija(MakeQuote("Galileo Renta Fija - Clase B", "Renta Fija", 1m, "ARS", new DateOnly(2026, 5, 15)));
        SetupOtherCategoriesEmpty();

        var result = await Build().GetCurrentPrice("FCI_doesnotexist");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Pricing.TickerNotFound");
    }

    [Fact]
    public async Task GetCurrentPrice_EmptySnapshot_ReturnsUpstreamFailure()
    {
        SetupAllCategoriesEmpty();

        var result = await Build().GetCurrentPrice("FCI_anything");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Pricing.Upstream");
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsCappedListSortedByFundName()
    {
        var quotes = Enumerable.Range(0, 25)
            .Select(i => MakeQuote($"Fondo {i:D2} - Clase A", "Renta Fija", 100m + i, "ARS", new DateOnly(2026, 5, 15)))
            .ToArray();
        SetupRentaFija(quotes);
        SetupOtherCategoriesEmpty();

        var result = await Build().Search(string.Empty);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(15);
        result.Value.Select(r => r.Description).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Search_SubstringMatch_IsCaseInsensitiveOnFundName()
    {
        SetupRentaFija(
            MakeQuote("Galileo Renta Fija - Clase B", "Renta Fija", 1m, "ARS", new DateOnly(2026, 5, 15)),
            MakeQuote("Alpha Pesos - Clase A", "Mercado Dinero", 2m, "ARS", new DateOnly(2026, 5, 15)));
        SetupOtherCategoriesEmpty();

        var result = await Build().Search("galileo");

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].Description.Should().Be("Galileo Renta Fija - Clase B (Renta Fija)");
    }

    [Fact]
    public async Task Search_SubstringMatch_CapsAt25Results()
    {
        var quotes = Enumerable.Range(0, 40)
            .Select(i => MakeQuote($"Match {i:D2}", "Renta Fija", 1m, "ARS", new DateOnly(2026, 5, 15)))
            .ToArray();
        SetupRentaFija(quotes);
        SetupOtherCategoriesEmpty();

        var result = await Build().Search("match");

        result.Value.Should().HaveCount(25);
    }

    [Fact]
    public async Task Search_EmptySnapshot_ReturnsEmptyList()
    {
        SetupAllCategoriesEmpty();

        var result = await Build().Search("anything");

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_DescriptionFormat_IncludesCategoryInParens()
    {
        var quote = MakeQuote("Cocos Acciones - Clase A", "Renta Variable", 1m, "ARS", new DateOnly(2026, 5, 15));
        SetupRentaVariable(quote);
        SetupRentaFijaEmpty();
        SetupRentaMixtaEmpty();
        SetupMercadoDineroEmpty();

        var result = await Build().Search("cocos");

        result.Value[0].Description.Should().Be("Cocos Acciones - Clase A (Renta Variable)");
    }

    [Theory]
    [InlineData("Galileo Renta Dolares - Clase A", "USD")]
    [InlineData("Pellegrini Renta DÓLARES - Clase B", "USD")]
    [InlineData("Some Fund USD Plus - Clase A", "USD")]
    [InlineData("Alpha Pesos - Clase A", "ARS")]
    [InlineData("Renta Mixta - Clase B", "ARS")]
    public void ResolveFciCurrency_UsdSubstringsReturnUsd(string fundName, string expected)
    {
        ArgentinaDatosClient.ResolveFciCurrency(fundName).Should().Be(expected);
    }

    [Fact]
    public void ComputeFciTicker_SameInput_SameOutput()
    {
        var a = ArgentinaDatosClient.ComputeFciTicker("Galileo Renta Fija - Clase B");
        var b = ArgentinaDatosClient.ComputeFciTicker("galileo  renta fija - clase b   ");

        a.Should().Be(b);
        a.Should().MatchRegex("^FCI_[0-9a-f]{10}$");
        a.Length.Should().Be(14);
    }

    [Fact]
    public void ComputeFciTicker_DifferentInputs_DifferentOutputs()
    {
        var a = ArgentinaDatosClient.ComputeFciTicker("Galileo Renta Fija - Clase A");
        var b = ArgentinaDatosClient.ComputeFciTicker("Galileo Renta Fija - Clase B");

        a.Should().NotBe(b);
    }

    private static FciQuote MakeQuote(string fundName, string category, decimal price, string currency, DateOnly asOf) =>
        new(ArgentinaDatosClient.ComputeFciTicker(fundName), fundName, category, price, currency, asOf);

    private void SetupAllCategoriesEmpty()
    {
        SetupMercadoDineroEmpty();
        SetupRentaFijaEmpty();
        SetupRentaVariableEmpty();
        SetupRentaMixtaEmpty();
    }

    private void SetupOtherCategoriesEmpty()
    {
        SetupMercadoDineroEmpty();
        SetupRentaVariableEmpty();
        SetupRentaMixtaEmpty();
    }

    private void SetupMercadoDineroEmpty() =>
        _client.Setup(c => c.GetFciMercadoDineroAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private void SetupRentaFijaEmpty() =>
        _client.Setup(c => c.GetFciRentaFijaAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private void SetupRentaVariableEmpty() =>
        _client.Setup(c => c.GetFciRentaVariableAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private void SetupRentaMixtaEmpty() =>
        _client.Setup(c => c.GetFciRentaMixtaAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    private void SetupRentaFija(params FciQuote[] quotes) =>
        _client.Setup(c => c.GetFciRentaFijaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(quotes);

    private void SetupRentaVariable(params FciQuote[] quotes) =>
        _client.Setup(c => c.GetFciRentaVariableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(quotes);
}
