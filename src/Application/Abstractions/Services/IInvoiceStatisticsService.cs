using Application.DTOs.Invoice.Statistics;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvoiceStatisticsService
{
    Task<ErrorOr<InvoiceCategoryProgress>> GetCategoryProgress(CancellationToken cancellationToken = default);
}
