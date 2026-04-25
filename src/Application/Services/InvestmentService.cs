using Application.Abstractions;
using Application.Abstractions.Pricing;
using Application.Abstractions.Services;
using Application.DTOs.Investment;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Services;

public sealed class InvestmentService(
    IInvestmentRepository investmentRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork,
    IExchangeRateCache exchangeRateCache,
    IUserCurrencyContext userCurrencyContext,
    IPriceProviderRegistry priceProviderRegistry) : IInvestmentService
{
    public async Task<ErrorOr<IReadOnlyList<InvestmentDetailResponse>>> GetAll(CancellationToken cancellationToken = default)
    {
        var investments = await investmentRepository.GetByUserId(currentUserProvider.UserId, cancellationToken);
        var context = await BuildValuationContext(cancellationToken);
        var results = new List<InvestmentDetailResponse>(investments.Count);
        foreach (var investment in investments)
        {
            results.Add(await Value(investment, context, cancellationToken));
        }
        return results;
    }

    public async Task<ErrorOr<InvestmentDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var investment = await investmentRepository.GetById(id, cancellationToken);
        if (investment is null || investment.UserId != currentUserProvider.UserId)
            return InvestmentErrors.NotFound;

        var context = await BuildValuationContext(cancellationToken);
        return await Value(investment, context, cancellationToken);
    }

    public async Task<ErrorOr<Investment>> Create(CreateInvestmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
            return InvestmentErrors.InvalidQuantity;

        if (!string.IsNullOrWhiteSpace(request.Ticker))
        {
            var provider = priceProviderRegistry.Get(request.AssetClass);
            var resolved = await provider.GetCurrentPrice(request.Ticker, cancellationToken);
            if (resolved.IsError && resolved.FirstError.Type == ErrorType.NotFound)
                return InvestmentErrors.UnknownTicker;
        }

        var investment = new Investment
        {
            UserId = currentUserProvider.UserId,
            AssetClass = request.AssetClass,
            Ticker = NormalizeTicker(request.Ticker),
            Description = request.Description,
            Quantity = request.Quantity,
            Date = request.Date,
            ManualYield = request.ManualYield
        };

        await investmentRepository.Add(investment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return investment;
    }

    public async Task<ErrorOr<Investment>> Update(Guid id, UpdateInvestmentRequest request, CancellationToken cancellationToken = default)
    {
        var investment = await investmentRepository.GetById(id, cancellationToken);
        if (investment is null || investment.UserId != currentUserProvider.UserId)
            return InvestmentErrors.NotFound;

        if (request.Quantity <= 0)
            return InvestmentErrors.InvalidQuantity;

        if (!string.IsNullOrWhiteSpace(request.Ticker))
        {
            var provider = priceProviderRegistry.Get(request.AssetClass);
            var resolved = await provider.GetCurrentPrice(request.Ticker, cancellationToken);
            if (resolved.IsError && resolved.FirstError.Type == ErrorType.NotFound)
                return InvestmentErrors.UnknownTicker;
        }

        investment.AssetClass = request.AssetClass;
        investment.Ticker = NormalizeTicker(request.Ticker);
        investment.Description = request.Description;
        investment.Quantity = request.Quantity;
        investment.Date = request.Date;
        investment.ManualYield = request.ManualYield;

        investmentRepository.Update(investment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return investment;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var investment = await investmentRepository.GetById(id, cancellationToken);
        if (investment is null || investment.UserId != currentUserProvider.UserId)
            return InvestmentErrors.NotFound;

        investmentRepository.Delete(investment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<InvestmentPortfolioResponse>> GetPortfolio(CancellationToken cancellationToken = default)
    {
        var investments = await investmentRepository.GetByUserId(currentUserProvider.UserId, cancellationToken);
        var context = await BuildValuationContext(cancellationToken);

        var totals = InitEmpty(context.DisplayCurrencies);
        var perClass = new Dictionary<AssetClass, Dictionary<string, decimal>>();
        var unvalued = 0;

        foreach (var investment in investments)
        {
            var response = await Value(investment, context, cancellationToken);
            if (response.ValuationStatus != ValuationStatus.Live || response.CurrentValue is null)
            {
                unvalued++;
                continue;
            }

            foreach (var (currency, value) in response.CurrentValue)
            {
                totals[currency] += value;
            }

            if (!perClass.TryGetValue(investment.AssetClass, out var classTotals))
            {
                classTotals = InitEmpty(context.DisplayCurrencies);
                perClass[investment.AssetClass] = classTotals;
            }
            foreach (var (currency, value) in response.CurrentValue)
            {
                classTotals[currency] += value;
            }
        }

        var breakdown = perClass
            .Select(kvp => new PortfolioAssetClassBreakdown(
                AssetClass: kvp.Key,
                CurrentValue: kvp.Value,
                WeightPct: ComputeWeights(kvp.Value, totals)))
            .ToList();

        return new InvestmentPortfolioResponse(totals, breakdown, unvalued);
    }

    private async Task<ValuationContext> BuildValuationContext(CancellationToken cancellationToken)
    {
        var lookup = await exchangeRateCache.GetLookupAsync(cancellationToken);
        var profile = await userCurrencyContext.ResolveAsync(cancellationToken);
        return new ValuationContext(lookup, profile.DisplayCurrencies);
    }

    private async Task<InvestmentDetailResponse> Value(Investment investment, ValuationContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(investment.Ticker))
        {
            return Map(investment, ValuationStatus.Unknown, currentPrice: null, currentCurrency: null, priceAsOf: null, currentValue: null);
        }

        var provider = priceProviderRegistry.Get(investment.AssetClass);
        var priceResult = await provider.GetCurrentPrice(investment.Ticker, cancellationToken);

        if (priceResult.IsError)
        {
            return Map(investment, ValuationStatus.Unknown, currentPrice: null, currentCurrency: null, priceAsOf: null, currentValue: null);
        }

        var price = priceResult.Value;
        var currentValueNative = investment.Quantity * price.Price;
        var currentValue = context.Lookup.ConvertToAll(currentValueNative, price.Currency, context.DisplayCurrencies, price.AsOf);

        return Map(investment, ValuationStatus.Live, price.Price, price.Currency, price.AsOf, currentValue);
    }

    private static InvestmentDetailResponse Map(
        Investment investment,
        ValuationStatus status,
        decimal? currentPrice,
        string? currentCurrency,
        DateOnly? priceAsOf,
        IReadOnlyDictionary<string, decimal>? currentValue) =>
        new(
            Id: investment.Id,
            UserId: investment.UserId,
            AssetClass: investment.AssetClass,
            Ticker: investment.Ticker,
            Description: investment.Description,
            Quantity: investment.Quantity,
            Date: investment.Date,
            ManualYield: investment.ManualYield,
            CurrentPrice: currentPrice,
            CurrentCurrency: currentCurrency,
            PriceAsOf: priceAsOf,
            ValuationStatus: status,
            CurrentValue: currentValue);

    private static Dictionary<string, decimal> InitEmpty(IEnumerable<string> currencies)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in currencies)
            result[currency] = 0m;
        return result;
    }

    private static IReadOnlyDictionary<string, decimal> ComputeWeights(
        IReadOnlyDictionary<string, decimal> classTotal,
        IReadOnlyDictionary<string, decimal> portfolioTotal)
    {
        var weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (currency, total) in portfolioTotal)
        {
            weights[currency] = total == 0m ? 0m : 100m * classTotal.GetValueOrDefault(currency) / total;
        }
        return weights;
    }

    private static string? NormalizeTicker(string? ticker) =>
        string.IsNullOrWhiteSpace(ticker) ? null : ticker.Trim().ToUpperInvariant();

    private sealed record ValuationContext(CurrencyLookup Lookup, IReadOnlyList<string> DisplayCurrencies);
}
