using Application.DTOs.Investment;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvestmentService
{
    Task<ErrorOr<IReadOnlyList<InvestmentDetailResponse>>> GetAll(CancellationToken cancellationToken = default);
    Task<ErrorOr<InvestmentDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Investment>> Create(CreateInvestmentRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Investment>> Update(Guid id, UpdateInvestmentRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<InvestmentPortfolioResponse>> GetPortfolio(CancellationToken cancellationToken = default);
}
