using System.Security.Claims;
using Application.Abstractions.Services;
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
    public async Task<ActionResult<IReadOnlyList<Expense>>> GetAll(CancellationToken cancellationToken)
        => MatchOk(await expenseService.GetAll(User.FindFirstValue("user_id"), cancellationToken));

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
}
