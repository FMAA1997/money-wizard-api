using Application.Abstractions.Pricing;
using Domain.Models;
using ErrorOr;

namespace Infrastructure.Pricing.Providers;

/// <summary>
/// FCI provider stub for v1. CAFCI exposes yield (%) but not cuotaparte unit prices
/// through its public rendimiento endpoint, so we can't compute a current value from
/// a user-held quantity of cuotapartes. For v1 we treat FCI holdings as opaque —
/// ValuationStatus = Unknown, and the UI shows the user's ManualYield.
/// A future revision can switch to a cuotaparte source (e.g. CNV or a richer CAFCI
/// endpoint) without changing the IPriceProvider contract.
/// </summary>
internal sealed class CafciFciProvider : IPriceProvider
{
    public AssetClass AssetClass => AssetClass.Fci;

    public Task<ErrorOr<AssetPrice>> GetCurrentPrice(string ticker, CancellationToken cancellationToken = default) =>
        Task.FromResult<ErrorOr<AssetPrice>>(Error.Failure(
            "Pricing.FciUnavailable",
            "FCI live pricing is not available in v1; use Manual Yield to display expected return."));

    public Task<ErrorOr<IReadOnlyList<AssetSearchResult>>> Search(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<ErrorOr<IReadOnlyList<AssetSearchResult>>>(Array.Empty<AssetSearchResult>());
}
