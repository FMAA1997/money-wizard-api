using Application.Abstractions.Services;
using Application.DTOs.Expense;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class ExpenseController(IExpenseService expenseService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDetailResponse>>> GetAll(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetAllInRange(startDate, endDate, cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarResponse<ExpenseCalendarRow>>> GetCalendar(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetCalendar(startDate, endDate, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpenseDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetById(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ExpenseSeries>> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.Create(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpenseSeries>> Update(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.Update(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.Delete(id, cancellationToken));

    [HttpPut("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateOccurrence(
        Guid id, DateOnly date, [FromBody] UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.UpdateOccurrence(id, date, request, cancellationToken));

    [HttpPut("{id:guid}/from/{date}")]
    public async Task<ActionResult<ExpenseSeries>> UpdateFromDate(
        Guid id, DateOnly date, [FromBody] UpdateExpenseFromDateRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.UpdateFromDate(id, date, request, cancellationToken));

    [HttpDelete("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.DeleteOccurrence(id, date, cancellationToken));

    [HttpDelete("{id:guid}/from/{date}")]
    public async Task<ActionResult> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.DeleteFromDate(id, date, cancellationToken));
}
