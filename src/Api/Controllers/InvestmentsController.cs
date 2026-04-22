using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Domain.Models;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public sealed class InvestmentsController(
    IInvestmentService investmentService,
    IInvestmentCatalogService catalogService) : ErrorController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvestmentDetailResponse>>> GetAll(CancellationToken cancellationToken)
        => MatchOk(await investmentService.GetAll(cancellationToken));

    [HttpGet("portfolio")]
    public async Task<ActionResult<InvestmentPortfolioResponse>> GetPortfolio(CancellationToken cancellationToken)
        => MatchOk(await investmentService.GetPortfolio(cancellationToken));

    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<AssetSearchResultResponse>>> Search(
        [FromQuery] AssetClass assetClass,
        [FromQuery] string q,
        CancellationToken cancellationToken)
        => MatchOk(await catalogService.Search(assetClass, q, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestmentDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => MatchOk(await investmentService.GetById(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Investment>> Create(
        [FromBody] CreateInvestmentRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await investmentService.Create(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Investment>> Update(
        Guid id,
        [FromBody] UpdateInvestmentRequest request,
        CancellationToken cancellationToken)
        => MatchOk(await investmentService.Update(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => MatchNoContent(await investmentService.Delete(id, cancellationToken));
}
