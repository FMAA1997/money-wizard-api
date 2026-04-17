using Domain.Models;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvoiceCategoryService
{
    Task<ErrorOr<IReadOnlyList<InvoiceCategory>>> GetAll(CancellationToken cancellationToken = default);
    Task<ErrorOr<InvoiceCategory>> GetById(Guid id, CancellationToken cancellationToken = default);
}
