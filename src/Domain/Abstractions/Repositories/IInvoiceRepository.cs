using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<IReadOnlyList<Invoice>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<Invoice?> GetException(Guid recurringInvoiceId, DateOnly originalDate, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid recurringInvoiceId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
