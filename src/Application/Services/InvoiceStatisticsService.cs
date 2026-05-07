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
            ? SumExpandedAmountsInArs(invoices, periodStart, invoicedEnd, scope)
            : 0m;
        var projectedAmount = SumExpandedAmountsInArs(invoices, periodStart, periodEnd, scope);

        var invoicedPercentage = category.Top != 0 ? invoicedAmount / category.Top * 100 : 0;
        var projectedPercentage = category.Top != 0 ? projectedAmount / category.Top * 100 : 0;

        return new InvoiceCategoryProgress(
            category.Id,
            category.Name,
            periodStart,
            periodEnd,
            category.Bottom,
            category.Top,
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

    private static decimal SumExpandedAmountsInArs(IReadOnlyList<Invoice> invoices, DateOnly startDate, DateOnly endDate, CurrencyScope scope)
    {
        var oneOffs = new List<Invoice>();
        var series = new List<Invoice>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Invoice>();

        foreach (var invoice in invoices)
        {
            if (invoice.RecurrenceRule is not null)
                series.Add(invoice);
            else if (invoice.RecurringInvoiceId.HasValue && invoice.OriginalDate.HasValue)
                exceptionLookup[(invoice.RecurringInvoiceId.Value, invoice.OriginalDate.Value)] = invoice;
            else
                oneOffs.Add(invoice);
        }

        var total = oneOffs
            .Where(i => i.Date >= startDate && i.Date <= endDate)
            .Sum(i => Sign(i.Type) * scope.Convert(i.Amount, i.Currency, "ARS", i.Date));

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);
            var sign = Sign(s.Type);

            foreach (var (date, _) in occurrences)
            {
                if (exceptionLookup.TryGetValue((s.Id, date), out var exception))
                {
                    if (!exception.IsDeleted)
                        total += sign * scope.Convert(exception.Amount, exception.Currency, "ARS", exception.Date);
                }
                else
                {
                    total += sign * scope.Convert(s.Amount, s.Currency, "ARS", date);
                }
            }
        }

        return total;
    }

    private static decimal Sign(InvoiceType type) =>
        type == InvoiceType.CreditNote ? -1m : 1m;
}
