using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class InvoiceCategoryRepository(MoneyWizardContext context) : IInvoiceCategoryRepository
{
    public async Task<InvoiceCategory?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.InvoiceCategories.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<InvoiceCategory>> GetAll(CancellationToken cancellationToken = default)
        => await context.InvoiceCategories.ToListAsync(cancellationToken);

    public async Task Add(InvoiceCategory entity, CancellationToken cancellationToken = default)
        => await context.InvoiceCategories.AddAsync(entity, cancellationToken);

    public void Update(InvoiceCategory entity)
        => context.InvoiceCategories.Update(entity);

    public void Delete(InvoiceCategory entity)
        => context.InvoiceCategories.Remove(entity);
}
