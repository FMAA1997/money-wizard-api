using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class ExpenseRepository(MoneyWizardContext context) : IExpenseRepository
{
    public async Task<Expense?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Expenses.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Expense>> GetAll(CancellationToken cancellationToken = default)
        => await context.Expenses.ToListAsync(cancellationToken);

    public async Task Add(Expense entity, CancellationToken cancellationToken = default)
        => await context.Expenses.AddAsync(entity, cancellationToken);

    public void Update(Expense entity)
        => context.Expenses.Update(entity);

    public void Delete(Expense entity)
        => context.Expenses.Remove(entity);

    public async Task<IReadOnlyList<Expense>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.Expenses.Where(e => e.UserId == userId).ToListAsync(cancellationToken);
}
