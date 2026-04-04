using System.Security.Claims;
using Application.Abstractions.Services;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/expense-categories")]
[Authorize]
public sealed class ExpenseCategoryController(IExpenseCategoryService expenseCategoryService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategory>>> GetAll(CancellationToken cancellationToken)
        => MatchOk(await expenseCategoryService.GetAll(User.FindFirstValue("user_id"), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpenseCategory>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await expenseCategoryService.GetById(User.FindFirstValue("user_id"), id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ExpenseCategory>> Create([FromBody] CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseCategoryService.Create(User.FindFirstValue("user_id"), request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpenseCategory>> Update(Guid id, [FromBody] UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
        => MatchOk(await expenseCategoryService.Update(User.FindFirstValue("user_id"), id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await expenseCategoryService.Delete(User.FindFirstValue("user_id"), id, cancellationToken));
}
