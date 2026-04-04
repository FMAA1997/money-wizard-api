using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class UserRepository(MoneyWizardContext context) : IUserRepository
{
    public async Task<User?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Users.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<User>> GetAll(CancellationToken cancellationToken = default)
        => await context.Users.ToListAsync(cancellationToken);

    public async Task Add(User entity, CancellationToken cancellationToken = default)
        => await context.Users.AddAsync(entity, cancellationToken);

    public void Update(User entity)
        => context.Users.Update(entity);

    public void Delete(User entity)
        => context.Users.Remove(entity);

    public async Task<User?> GetByExternalId(string externalId, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

    public async Task<bool> ExistsByEmail(string email, CancellationToken cancellationToken = default)
        => await context.Users.AnyAsync(u => u.Email == email, cancellationToken);
}
