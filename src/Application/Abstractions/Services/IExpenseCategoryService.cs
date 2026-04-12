using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseCategoryService
{
    Task<ErrorOr<IReadOnlyList<ExpenseCategory>>> GetAll(CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> Create(CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseCategory>> Update(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default);
}
