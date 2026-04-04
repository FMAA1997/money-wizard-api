using System.Security.Claims;
using Application.Abstractions.Services;
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
    public async Task<ActionResult<IReadOnlyList<Paycheck>>> GetAll(CancellationToken cancellationToken)
        => MatchOk(await paycheckService.GetAll(User.FindFirstValue("user_id"), cancellationToken));

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
}
