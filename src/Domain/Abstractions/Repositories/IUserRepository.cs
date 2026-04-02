using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
