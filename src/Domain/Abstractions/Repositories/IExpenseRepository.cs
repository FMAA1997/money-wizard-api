using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IReadOnlyList<Expense>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
}
