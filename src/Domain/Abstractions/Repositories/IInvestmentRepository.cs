using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IInvestmentRepository : IRepository<Investment>
{
    Task<IReadOnlyList<Investment>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
}
