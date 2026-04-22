using Application.DTOs.Investment;
using Domain.Models;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvestmentCatalogService
{
    Task<ErrorOr<IReadOnlyList<AssetSearchResultResponse>>> Search(AssetClass assetClass, string query, CancellationToken cancellationToken = default);
}
