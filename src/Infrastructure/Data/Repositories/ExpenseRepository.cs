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

    public async Task<IReadOnlyList<Expense>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => await context.Expenses
            .Include(e => e.Category)
            .Include(e => e.Paycheck)
            .Include(e => e.Invoice)
            .Where(e => e.UserId == userId
                && (
                    (e.Date >= startDate && e.Date <= endDate)
                    || (e.RecurrenceRule != null && e.Date < startDate
                        && (e.RecurrenceRule!.EndDate == null || e.RecurrenceRule!.EndDate >= startDate))
                ))
            .ToListAsync(cancellationToken);

    public async Task<Expense?> GetException(Guid recurringExpenseId, DateOnly originalDate, CancellationToken cancellationToken = default)
        => await context.Expenses
            .FirstOrDefaultAsync(e => e.RecurringExpenseId == recurringExpenseId
                && e.OriginalDate == originalDate, cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid recurringExpenseId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.Expenses
            .Where(e => e.RecurringExpenseId == recurringExpenseId
                && e.OriginalDate != null
                && e.OriginalDate >= fromDate)
            .ExecuteDeleteAsync(cancellationToken);
}
