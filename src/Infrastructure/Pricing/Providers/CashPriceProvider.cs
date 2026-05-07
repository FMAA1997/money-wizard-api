using Application.Abstractions.Pricing;
using Domain.Models;
using ErrorOr;

namespace Infrastructure.Pricing.Providers;

internal sealed class CashPriceProvider : IPriceProvider
{
    private static readonly IReadOnlyList<AssetSearchResult> Currencies =
    [
        new("USD", "US Dollar", "USD"),
        new("ARS", "Argentine Peso", "ARS"),
        new("EUR", "Euro", "EUR"),
        new("BRL", "Brazilian Real", "BRL"),
        new("GBP", "British Pound", "GBP"),
        new("CLP", "Chilean Peso", "CLP"),
        new("UYU", "Uruguayan Peso", "UYU"),
    ];

    public AssetClass AssetClass => AssetClass.Cash;

    public Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default)
    {
        var code = ticker.Trim().ToUpperInvariant();
        var price = new AssetPrice(code, 1m, code, DateOnly.FromDateTime(DateTime.UtcNow));
        return Task.FromResult<ErrorOr<AssetPrice>>(price);
    }

    public Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        IReadOnlyList<AssetSearchResult> matches = string.IsNullOrEmpty(q)
            ? Currencies
            : Currencies
                .Where(c =>
                    c.Ticker.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        return Task.FromResult(matches.ToErrorOr());
    }
}
