using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IReadOnlyList<Expense>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<Expense?> GetException(Guid recurringExpenseId, DateOnly originalDate, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid recurringExpenseId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
