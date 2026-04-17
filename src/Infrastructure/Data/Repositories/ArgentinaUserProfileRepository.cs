using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class ArgentinaUserProfileRepository(MoneyWizardContext context) : IArgentinaUserProfileRepository
{
    public async Task<ArgentinaUserProfile?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.ArgentinaUserProfiles.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<ArgentinaUserProfile>> GetAll(CancellationToken cancellationToken = default)
        => await context.ArgentinaUserProfiles.ToListAsync(cancellationToken);

    public async Task Add(ArgentinaUserProfile entity, CancellationToken cancellationToken = default)
        => await context.ArgentinaUserProfiles.AddAsync(entity, cancellationToken);

    public void Update(ArgentinaUserProfile entity)
        => context.ArgentinaUserProfiles.Update(entity);

    public void Delete(ArgentinaUserProfile entity)
        => context.ArgentinaUserProfiles.Remove(entity);

    public async Task<ArgentinaUserProfile?> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.ArgentinaUserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
}
