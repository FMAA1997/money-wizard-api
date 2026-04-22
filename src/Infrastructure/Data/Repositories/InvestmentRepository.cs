using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class InvestmentRepository(MoneyWizardContext context) : IInvestmentRepository
{
    public async Task<Investment?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Investments.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Investment>> GetAll(CancellationToken cancellationToken = default)
        => await context.Investments.ToListAsync(cancellationToken);

    public async Task Add(Investment entity, CancellationToken cancellationToken = default)
        => await context.Investments.AddAsync(entity, cancellationToken);

    public void Update(Investment entity)
        => context.Investments.Update(entity);

    public void Delete(Investment entity)
        => context.Investments.Remove(entity);

    public async Task<IReadOnlyList<Investment>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.Investments
            .Where(i => i.UserId == userId)
            .ToListAsync(cancellationToken);
}
