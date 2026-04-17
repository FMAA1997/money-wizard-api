using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class InvoiceRepository(MoneyWizardContext context) : IInvoiceRepository
{
    public async Task<Invoice?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Invoices.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetAll(CancellationToken cancellationToken = default)
        => await context.Invoices.ToListAsync(cancellationToken);

    public async Task Add(Invoice entity, CancellationToken cancellationToken = default)
        => await context.Invoices.AddAsync(entity, cancellationToken);

    public void Update(Invoice entity)
        => context.Invoices.Update(entity);

    public void Delete(Invoice entity)
        => context.Invoices.Remove(entity);

    public async Task<IReadOnlyList<Invoice>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.Invoices.Where(i => i.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => await context.Invoices
            .Include(i => i.Paycheck)
            .Where(i => i.UserId == userId
                && (
                    (i.Date >= startDate && i.Date <= endDate)
                    || (i.RecurrenceRule != null && i.Date < startDate
                        && (i.RecurrenceRule!.EndDate == null || i.RecurrenceRule!.EndDate >= startDate))
                ))
            .ToListAsync(cancellationToken);

    public async Task<Invoice?> GetException(Guid recurringInvoiceId, DateOnly originalDate, CancellationToken cancellationToken = default)
        => await context.Invoices
            .FirstOrDefaultAsync(i => i.RecurringInvoiceId == recurringInvoiceId
                && i.OriginalDate == originalDate, cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid recurringInvoiceId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.Invoices
            .Where(i => i.RecurringInvoiceId == recurringInvoiceId
                && i.OriginalDate != null
                && i.OriginalDate >= fromDate)
            .ExecuteDeleteAsync(cancellationToken);
}
