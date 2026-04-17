using Application.Abstractions.Services;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class InvoiceCategoryService(
    IInvoiceCategoryRepository invoiceCategoryRepository) : IInvoiceCategoryService
{
    public async Task<ErrorOr<IReadOnlyList<InvoiceCategory>>> GetAll(CancellationToken cancellationToken = default)
    {
        var categories = await invoiceCategoryRepository.GetAll(cancellationToken);
        return categories.ToList();
    }

    public async Task<ErrorOr<InvoiceCategory>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await invoiceCategoryRepository.GetById(id, cancellationToken);
        if (category is null)
            return InvoiceCategoryErrors.NotFound;

        return category;
    }
}
