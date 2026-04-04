using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class ExpenseCategoryRepository(MoneyWizardContext context) : IExpenseCategoryRepository
{
    public async Task<ExpenseCategory?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.ExpenseCategories.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<ExpenseCategory>> GetAll(CancellationToken cancellationToken = default)
        => await context.ExpenseCategories.ToListAsync(cancellationToken);

    public async Task Add(ExpenseCategory entity, CancellationToken cancellationToken = default)
        => await context.ExpenseCategories.AddAsync(entity, cancellationToken);

    public void Update(ExpenseCategory entity)
        => context.ExpenseCategories.Update(entity);

    public void Delete(ExpenseCategory entity)
        => context.ExpenseCategories.Remove(entity);

    public async Task<IReadOnlyList<ExpenseCategory>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.ExpenseCategories.Where(ec => ec.UserId == userId).ToListAsync(cancellationToken);
}
