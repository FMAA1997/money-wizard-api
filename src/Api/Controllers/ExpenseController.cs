using System.Security.Claims;
using Application.Abstractions.Services;
using Application.DTOs.Expense;
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
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> GetAll(
        [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetAllInRange(User.FindFirstValue("user_id"), startDate, endDate, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Expense>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetById(User.FindFirstValue("user_id"), id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Expense>> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.Create(User.FindFirstValue("user_id"), request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Expense>> Update(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.Update(User.FindFirstValue("user_id"), id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.Delete(User.FindFirstValue("user_id"), id, cancellationToken));

    [HttpPut("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateOccurrence(
        Guid id, DateOnly date, [FromBody] UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.UpdateOccurrence(User.FindFirstValue("user_id"), id, date, request, cancellationToken));

    [HttpPut("{id:guid}/from/{date}")]
    public async Task<ActionResult<Expense>> UpdateFromDate(
        Guid id, DateOnly date, [FromBody] UpdateExpenseRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseService.UpdateFromDate(User.FindFirstValue("user_id"), id, date, request, cancellationToken));

    [HttpDelete("{id:guid}/occurrence/{date}")]
    public async Task<ActionResult> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.DeleteOccurrence(User.FindFirstValue("user_id"), id, date, cancellationToken));

    [HttpDelete("{id:guid}/from/{date}")]
    public async Task<ActionResult> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken)
        => MatchNoContent(await expenseService.DeleteFromDate(User.FindFirstValue("user_id"), id, date, cancellationToken));
}
