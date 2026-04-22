using Application.Abstractions.Services;
using Application.DTOs.Paycheck;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class PaychecksController(IPaycheckService paycheckService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaycheckDetailResponse>>> GetAll(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetAllInRange(startDate, endDate, cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarResponse<PaycheckCalendarRow>>> GetCalendar(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetCalendar(startDate, endDate, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaycheckDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetById(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Paycheck>> Create([FromBody] CreatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.Create(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Paycheck>> Update(Guid id, [FromBody] UpdatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.Update(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.Delete(id, cancellationToken));

    [HttpPut("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult<PaycheckResponse>> UpdateOccurrence(
        Guid id, DateOnly date, [FromBody] UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.UpdateOccurrence(id, date, request, cancellationToken));

    [HttpPut("{id:guid}/from/{date}")]
    public async Task<ActionResult<Paycheck>> UpdateFromDate(
        Guid id, DateOnly date, [FromBody] UpdatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.UpdateFromDate(id, date, request, cancellationToken));

    [HttpDelete("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.DeleteOccurrence(id, date, cancellationToken));

    [HttpDelete("{id:guid}/from/{date}")]
    public async Task<ActionResult> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.DeleteFromDate(id, date, cancellationToken));
}
