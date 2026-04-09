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

    public async Task<IReadOnlyList<Paycheck>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => await context.Paychecks
            .Where(p => p.UserId == userId
                && (
                    (p.Date >= startDate && p.Date <= endDate)
                    || (p.RecurrenceRule != null && p.Date < startDate
                        && (p.RecurrenceRule!.EndDate == null || p.RecurrenceRule!.EndDate >= startDate))
                ))
            .ToListAsync(cancellationToken);

    public async Task<Paycheck?> GetException(Guid recurringPaycheckId, DateOnly originalDate, CancellationToken cancellationToken = default)
        => await context.Paychecks
            .FirstOrDefaultAsync(p => p.RecurringPaycheckId == recurringPaycheckId
                && p.OriginalDate == originalDate, cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid recurringPaycheckId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.Paychecks
            .Where(p => p.RecurringPaycheckId == recurringPaycheckId
                && p.OriginalDate != null
                && p.OriginalDate >= fromDate)
            .ExecuteDeleteAsync(cancellationToken);
}
