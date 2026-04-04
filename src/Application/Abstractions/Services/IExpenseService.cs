using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseService
{
    Task<ErrorOr<IReadOnlyList<Expense>>> GetAll(string? externalId, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Create(string? externalId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Update(string? externalId, Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default);
}
