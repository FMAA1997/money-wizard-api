using Application.Abstractions.Services;
using Application.DTOs.Invoice.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/invoices/statistics")]
[Authorize]
public sealed class InvoiceStatisticsController(IInvoiceStatisticsService invoiceStatisticsService) : ErrorController
{
    [HttpGet("category-progress")]
    public async Task<ActionResult<InvoiceCategoryProgress>> GetCategoryProgress(CancellationToken cancellationToken) =>
        MatchOk(await invoiceStatisticsService.GetCategoryProgress(cancellationToken));
}
