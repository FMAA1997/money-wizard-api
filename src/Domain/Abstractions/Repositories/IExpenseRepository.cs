using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IExpenseRepository : IRepository<ExpenseSeries>
{
    Task<IReadOnlyList<ExpenseSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    Task AddSegment(ExpenseSegment segment, CancellationToken cancellationToken = default);
    void UpdateSegment(ExpenseSegment segment);
    void DeleteSegment(ExpenseSegment segment);

    Task AddException(ExpenseException exception, CancellationToken cancellationToken = default);
    void UpdateException(ExpenseException exception);
    void DeleteException(ExpenseException exception);

    Task<ExpenseException?> GetException(Guid seriesId, DateOnly date, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
    Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
