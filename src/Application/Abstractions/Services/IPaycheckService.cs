using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IPaycheckService
{
    Task<ErrorOr<IReadOnlyList<Paycheck>>> GetAll(string? externalId, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> Create(string? externalId, CreatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> Update(string? externalId, Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default);
}
