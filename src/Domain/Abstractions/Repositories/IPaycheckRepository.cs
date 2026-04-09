using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IPaycheckRepository : IRepository<Paycheck>
{
    Task<IReadOnlyList<Paycheck>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Paycheck>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<Paycheck?> GetException(Guid recurringPaycheckId, DateOnly originalDate, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid recurringPaycheckId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
