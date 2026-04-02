using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Users.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Users.ToListAsync(cancellationToken);

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        context.Users.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        context.Users.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        context.Users.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await context.Users.AnyAsync(u => u.Email == email, cancellationToken);
}
