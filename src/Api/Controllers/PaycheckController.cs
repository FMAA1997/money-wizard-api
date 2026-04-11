using System.Security.Claims;
using Application.Abstractions.Services;
using Application.DTOs.Paycheck;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class PaycheckController(IPaycheckService paycheckService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Paycheck>>> GetAll(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetAllInRange(User.FindFirstValue("user_id"), startDate, endDate, cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<PaycheckCalendarResponse>> GetCalendar(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetCalendar(User.FindFirstValue("user_id"), startDate, endDate, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Paycheck>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetById(User.FindFirstValue("user_id"), id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Paycheck>> Create([FromBody] CreatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.Create(User.FindFirstValue("user_id"), request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Paycheck>> Update(Guid id, [FromBody] UpdatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.Update(User.FindFirstValue("user_id"), id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.Delete(User.FindFirstValue("user_id"), id, cancellationToken));

    [HttpPut("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult<PaycheckResponse>> UpdateOccurrence(
        Guid id, DateOnly date, [FromBody] UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.UpdateOccurrence(User.FindFirstValue("user_id"), id, date, request, cancellationToken));

    [HttpPut("{id:guid}/from/{date}")]
    public async Task<ActionResult<Paycheck>> UpdateFromDate(
        Guid id, DateOnly date, [FromBody] UpdatePaycheckRequest request, CancellationToken cancellationToken)
        => MatchOk(await paycheckService.UpdateFromDate(User.FindFirstValue("user_id"), id, date, request, cancellationToken));

    [HttpDelete("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.DeleteOccurrence(User.FindFirstValue("user_id"), id, date, cancellationToken));

    [HttpDelete("{id:guid}/from/{date}")]
    public async Task<ActionResult> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await paycheckService.DeleteFromDate(User.FindFirstValue("user_id"), id, date, cancellationToken));
}
