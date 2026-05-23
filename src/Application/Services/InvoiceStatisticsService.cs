using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Invoice.Statistics;
using Application.Services.Currency;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class InvoiceStatisticsService(
    IUserRepository userRepository,
    IInvoiceRepository invoiceRepository,
    IInvoiceCategoryRepository invoiceCategoryRepository,
    ICountryProfileRegistry registry,
    ICurrentUserProvider currentUserProvider,
    ICurrencyConverter currencyConverter) : IInvoiceStatisticsService
{
    private const string ThresholdCurrency = "ARS";

    public async Task<ErrorOr<InvoiceCategoryProgress>> GetCategoryProgress(CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetById(currentUserProvider.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        if (!registry.TryGet(user.Country, out var handler))
            return ProfileErrors.InvoiceCategoryNotConfigured;

        var categoryIdResult = await handler.GetInvoiceCategoryId(user.Id, cancellationToken);
        if (categoryIdResult.IsError)
            return categoryIdResult.Errors;

        var category = await invoiceCategoryRepository.GetById(categoryIdResult.Value, cancellationToken);
        if (category is null)
            return ProfileErrors.InvoiceCategoryNotConfigured;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = ComputeLastCutDate(category.CutDate, today);
        var periodEnd = periodStart.AddYears(1);

        var invoices = await invoiceRepository.GetByUserIdInRange(user.Id, periodStart, periodEnd, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        var invoicedEnd = today < periodEnd ? today : periodEnd;
        var invoicedAmount = invoicedEnd >= periodStart
            ? SumExpandedAmounts(invoices, periodStart, invoicedEnd, scope)
            : scope.DisplayCurrencies.ToDictionary(c => c, _ => 0m);
        var projectedAmount = SumExpandedAmounts(invoices, periodStart, periodEnd, scope);

        var categoryBottom = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => scope.Convert(category.Bottom, ThresholdCurrency, c, today));
        var categoryTop = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => scope.Convert(category.Top, ThresholdCurrency, c, today));

        var invoicedPercentage = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => categoryTop[c] != 0 ? invoicedAmount[c] / categoryTop[c] * 100 : 0m);
        var projectedPercentage = scope.DisplayCurrencies.ToDictionary(
            c => c,
            c => categoryTop[c] != 0 ? projectedAmount[c] / categoryTop[c] * 100 : 0m);

        return new InvoiceCategoryProgress(
            category.Id,
            category.Name,
            periodStart,
            periodEnd,
            categoryBottom,
            categoryTop,
            invoicedAmount,
            projectedAmount,
            invoicedPercentage,
            projectedPercentage);
    }

    private static DateOnly ComputeLastCutDate(DateOnly cutDate, DateOnly today)
    {
        var candidate = SafeDate(today.Year, cutDate.Month, cutDate.Day);
        return candidate <= today
            ? candidate
            : SafeDate(today.Year - 1, cutDate.Month, cutDate.Day);
    }

    private static DateOnly SafeDate(int year, int month, int day)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, maxDay));
    }

    private static IReadOnlyDictionary<string, decimal> SumExpandedAmounts(
        IReadOnlyList<InvoiceSeries> seriesList, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var totals = scope.NewTotals();
        foreach (var series in seriesList)
        {
            var sign = Sign(series.Type);
            foreach (var occurrence in InvoiceSeriesExpander.Expand(series, startDate, endDate))
            {
                var amount = occurrence.Exception?.Amount ?? occurrence.Segment!.Amount;
                var currency = occurrence.Exception?.Currency ?? occurrence.Segment!.Currency;
                totals.Add(sign * amount, currency, occurrence.Date);
            }
        }
        return totals.ToDictionary();
    }

    private static decimal Sign(InvoiceType type) =>
        type == InvoiceType.CreditNote ? -1m : 1m;
}
