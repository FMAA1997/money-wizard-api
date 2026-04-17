using Domain.Models;

namespace Domain.Abstractions.Repositories;

public interface IArgentinaUserProfileRepository : IRepository<ArgentinaUserProfile>
{
    Task<ArgentinaUserProfile?> GetByUserId(Guid userId, CancellationToken cancellationToken = default);
}
