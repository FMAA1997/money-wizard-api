using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IPaycheckRepository : IRepository<Paycheck>
{
    Task<IReadOnlyList<Paycheck>> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
}
