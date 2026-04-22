using Domain.Models;
using ErrorOr;

namespace Application.Abstractions.Pricing;

public interface IPriceProvider
{
    AssetClass AssetClass { get; }

    Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default);

    Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default);
}
