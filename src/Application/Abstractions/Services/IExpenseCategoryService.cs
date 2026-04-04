using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseCategoryService
{
    Task<ErrorOr<IReadOnlyList<ExpenseCategory>>> GetAll(string? externalId, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> Create(string? externalId, CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> Update(string? externalId, Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default);
}
