using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IExpenseCategoryRepository : IRepository<ExpenseCategory>
{
    Task<IReadOnlyList<ExpenseCategory>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
}
