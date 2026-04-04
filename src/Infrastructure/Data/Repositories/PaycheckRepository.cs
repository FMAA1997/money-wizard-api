using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class PaycheckRepository(MoneyWizardContext context) : IPaycheckRepository
{
    public async Task<Paycheck?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Paychecks.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Paycheck>> GetAll(CancellationToken cancellationToken = default)
        => await context.Paychecks.ToListAsync(cancellationToken);

    public async Task Add(Paycheck entity, CancellationToken cancellationToken = default)
        => await context.Paychecks.AddAsync(entity, cancellationToken);

    public void Update(Paycheck entity)
        => context.Paychecks.Update(entity);

    public void Delete(Paycheck entity)
        => context.Paychecks.Remove(entity);

    public async Task<IReadOnlyList<Paycheck>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.Paychecks.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
}
