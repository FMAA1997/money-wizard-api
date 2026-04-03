using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByExternalId(string externalId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmail(string email, CancellationToken cancellationToken = default);
}
