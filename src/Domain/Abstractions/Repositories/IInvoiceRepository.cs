using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IInvoiceRepository : IRepository<InvoiceSeries>
{
    Task<IReadOnlyList<InvoiceSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<bool> AnyForUser(Guid userId, CancellationToken cancellationToken = default);

    Task AddSegment(InvoiceSegment segment, CancellationToken cancellationToken = default);
    void UpdateSegment(InvoiceSegment segment);
    void DeleteSegment(InvoiceSegment segment);

    Task AddException(InvoiceException exception, CancellationToken cancellationToken = default);
    void UpdateException(InvoiceException exception);

    Task<InvoiceException?> GetException(Guid seriesId, DateOnly originalDate, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
    Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
