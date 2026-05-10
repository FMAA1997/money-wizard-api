using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IPaycheckRepository : IRepository<PaycheckSeries>
{
    Task<IReadOnlyList<PaycheckSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaycheckSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    Task AddSegment(PaycheckSegment segment, CancellationToken cancellationToken = default);
    void UpdateSegment(PaycheckSegment segment);
    void DeleteSegment(PaycheckSegment segment);

    Task AddException(PaycheckException exception, CancellationToken cancellationToken = default);
    void UpdateException(PaycheckException exception);

    Task<PaycheckException?> GetException(Guid seriesId, DateOnly originalDate, CancellationToken cancellationToken = default);
    Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
    Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default);
}
