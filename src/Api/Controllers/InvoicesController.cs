using Application.Abstractions.Services;
using Application.DTOs.Invoice;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class InvoicesController(IInvoiceService invoiceService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Invoice>>> GetAll(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.GetAllInRange(startDate, endDate, cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarResponse<InvoiceCalendarRow>>> GetCalendar(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.GetCalendar(startDate, endDate, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Invoice>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.GetById(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Invoice>> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.Create(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Invoice>> Update(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.Update(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await invoiceService.Delete(id, cancellationToken));

    [HttpPut("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult<InvoiceResponse>> UpdateOccurrence(
        Guid id, DateOnly date, [FromBody] UpdateInvoiceOccurrenceRequest request, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.UpdateOccurrence(id, date, request, cancellationToken));

    [HttpPut("{id:guid}/from/{date}")]
    public async Task<ActionResult<Invoice>> UpdateFromDate(
        Guid id, DateOnly date, [FromBody] UpdateInvoiceRequest request, CancellationToken cancellationToken)
        => MatchOk(await invoiceService.UpdateFromDate(id, date, request, cancellationToken));

    [HttpDelete("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await invoiceService.DeleteOccurrence(id, date, cancellationToken));

    [HttpDelete("{id:guid}/from/{date}")]
    public async Task<ActionResult> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await invoiceService.DeleteFromDate(id, date, cancellationToken));
}
