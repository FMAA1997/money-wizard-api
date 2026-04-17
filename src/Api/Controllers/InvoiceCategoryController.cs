using Application.Abstractions.Services;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/invoice-categories")]
[Authorize]
public sealed class InvoiceCategoryController(IInvoiceCategoryService invoiceCategoryService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceCategory>>> GetAll(CancellationToken cancellationToken)
        => MatchOk(await invoiceCategoryService.GetAll(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceCategory>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await invoiceCategoryService.GetById(id, cancellationToken));
}
