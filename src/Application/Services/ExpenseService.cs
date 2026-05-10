using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Expense;
using Application.DTOs.Invoice;
using Application.DTOs.Paycheck;
using Application.DTOs.Shared;
using Application.Services.Currency;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using Domain.Services;
using ErrorOr;

namespace Application.Services;

public sealed class ExpenseService(
    IExpenseRepository expenseRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork,
    ICurrencyConverter currencyConverter) : IExpenseService
{
    public async Task<ErrorOr<IReadOnlyList<ExpenseDetailResponse>>> GetAllInRange(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);

        return seriesList.Select(MapDetail).ToList();
    }

    public async Task<ErrorOr<ExpenseDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        return MapDetail(series);
    }

    public async Task<ErrorOr<ExpenseSeries>> Create(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var series = new ExpenseSeries
        {
            UserId = currentUserProvider.UserId,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Segments =
            {
                new ExpenseSegment
                {
                    EffectiveFrom = request.Date,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    PaycheckSeriesId = request.PaycheckSeriesId,
                    InvoiceSeriesId = request.InvoiceSeriesId,
                    RecurrenceRule = MapRecurrence(request.Recurrence)
                }
            }
        };

        await expenseRepository.Add(series, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<ExpenseSeries>> Update(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        series.Description = request.Description;
        series.CategoryId = request.CategoryId;

        expenseRepository.Update(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return series;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        expenseRepository.Delete(series);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<ExpenseResponse>> UpdateOccurrence(
        Guid id, DateOnly date, UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        var existing = await expenseRepository.GetException(series.Id, date, cancellationToken);
        var isRecurrence = ExpenseSeriesExpander.IsRecurrenceOccurrence(series, date);

        if (!isRecurrence && existing is null)
        {
            if (request.Amount is null || string.IsNullOrEmpty(request.Currency))
                return RecurrenceErrors.InsertionRequiresAmountAndCurrency;
        }

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        ExpenseException exception;
        ExpenseSegment? segment;
        int occurrenceIndex;

        if (existing is not null && !existing.OriginalDate.HasValue)
        {
            existing.Date = date;
            existing.Amount = request.Amount;
            existing.Currency = request.Currency;
            expenseRepository.UpdateException(existing);
            exception = existing;
            segment = null;
            occurrenceIndex = -1;
        }
        else if (existing is not null)
        {
            existing.Date = request.Date;
            existing.Amount = request.Amount;
            existing.Currency = request.Currency;
            existing.IsDeleted = false;
            expenseRepository.UpdateException(existing);
            exception = existing;
            segment = ExpenseSeriesExpander.GetSegmentForDate(series, date)!;
            occurrenceIndex = segment.RecurrenceRule is null
                ? 0
                : RecurrenceExpander.GetOccurrenceIndex(segment.EffectiveFrom, segment.RecurrenceRule, date);
        }
        else if (isRecurrence)
        {
            exception = new ExpenseException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                Date = request.Date,
                Amount = request.Amount,
                Currency = request.Currency,
                IsDeleted = false
            };
            await expenseRepository.AddException(exception, cancellationToken);
            segment = ExpenseSeriesExpander.GetSegmentForDate(series, date)!;
            occurrenceIndex = segment.RecurrenceRule is null
                ? 0
                : RecurrenceExpander.GetOccurrenceIndex(segment.EffectiveFrom, segment.RecurrenceRule, date);
        }
        else
        {
            exception = new ExpenseException
            {
                SeriesId = series.Id,
                OriginalDate = null,
                Date = date,
                Amount = request.Amount,
                Currency = request.Currency,
                IsDeleted = false
            };
            await expenseRepository.AddException(exception, cancellationToken);
            segment = null;
            occurrenceIndex = -1;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var occurrence = new ExpenseOccurrence(
            Date: exception.Date ?? date,
            OriginalDate: exception.OriginalDate,
            Segment: segment,
            Exception: exception,
            OccurrenceIndex: occurrenceIndex);

        return MapOccurrence(occurrence, series, scope);
    }

    public async Task<ErrorOr<ExpenseSeries>> UpdateFromDate(
        Guid id, DateOnly date, UpdateExpenseFromDateRequest request, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var segment = ExpenseSeriesExpander.GetSegmentForDate(series, date);

        unitOfWork.BeginTransaction();
        if (segment is not null)
        {
            CapOrDeleteSegment(segment, date);
            await expenseRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
            await expenseRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);
        }

        var newSegment = new ExpenseSegment
        {
            SeriesId = series.Id,
            EffectiveFrom = date,
            Amount = request.Amount,
            Currency = request.Currency,
            PaycheckSeriesId = request.PaycheckSeriesId,
            InvoiceSeriesId = request.InvoiceSeriesId,
            RecurrenceRule = MapRecurrence(request.Recurrence)
        };

        await expenseRepository.AddSegment(newSegment, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return (await expenseRepository.GetById(series.Id, cancellationToken))!;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        var existing = await expenseRepository.GetException(series.Id, date, cancellationToken);

        if (existing is not null && !existing.OriginalDate.HasValue)
        {
            expenseRepository.DeleteException(existing);
        }
        else if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.Date = null;
            existing.Amount = null;
            existing.Currency = null;
            expenseRepository.UpdateException(existing);
        }
        else if (ExpenseSeriesExpander.IsRecurrenceOccurrence(series, date))
        {
            var exception = new ExpenseException
            {
                SeriesId = series.Id,
                OriginalDate = date,
                IsDeleted = true
            };
            await expenseRepository.AddException(exception, cancellationToken);
        }
        else
        {
            return ExpenseErrors.NotFound;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        var segment = ExpenseSeriesExpander.GetSegmentForDate(series, date);
        if (segment is null)
            return ExpenseErrors.NotFound;

        unitOfWork.BeginTransaction();
        CapOrDeleteSegment(segment, date);

        await expenseRepository.DeleteSegmentsFromDate(series.Id, date, cancellationToken);
        await expenseRepository.DeleteExceptionsFromDate(series.Id, date, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        var refreshed = await expenseRepository.GetById(series.Id, cancellationToken);
        if (refreshed is not null && refreshed.Segments.Count == 0)
        {
            expenseRepository.Delete(refreshed);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Deleted;
    }

    public async Task<ErrorOr<CalendarResponse<ExpenseCalendarRow>>> GetCalendar(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var seriesList = await expenseRepository.GetByUserIdInRange(
            currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var months = new List<string>();
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);
        while (current <= end)
        {
            months.Add(current.ToString("yyyy-MM"));
            current = current.AddMonths(1);
        }

        var rows = new List<ExpenseCalendarRow>();
        foreach (var series in seriesList)
        {
            var occurrences = ExpenseSeriesExpander.Expand(series, startDate, endDate);
            if (occurrences.Count == 0) continue;

            var responses = occurrences
                .Select(o => MapOccurrence(o, series, scope))
                .OrderBy(r => r.Date)
                .ToList();

            var monthDict = responses
                .GroupBy(r => r.Date.ToString("yyyy-MM"))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ExpenseResponse>)[.. g.OrderBy(o => o.Date)]);

            var activeSegment = ExpenseSeriesExpander.GetActiveSegment(series, today);
            var isRecurring = series.Segments.Any(s => s.RecurrenceRule is not null);
            var recurrence = activeSegment?.RecurrenceRule is null
                ? null
                : new RecurrenceInfo(
                    activeSegment.EffectiveFrom,
                    activeSegment.RecurrenceRule.Frequency,
                    activeSegment.RecurrenceRule.Interval,
                    activeSegment.RecurrenceRule.EndDate,
                    activeSegment.RecurrenceRule.TotalInstallments);

            var category = series.Category;
            var source = activeSegment?.PaycheckSeries is not null
                ? new ExpenseSourceInfo(
                    activeSegment.PaycheckSeries.Id,
                    activeSegment.PaycheckSeries.Description,
                    0m,
                    "Paycheck")
                : activeSegment?.InvoiceSeries is not null
                ? new ExpenseSourceInfo(
                    activeSegment.InvoiceSeries.Id,
                    activeSegment.InvoiceSeries.Description,
                    0m,
                    "Invoice")
                : null;

            rows.Add(new ExpenseCalendarRow(
                ExpenseId: series.Id,
                Description: series.Description,
                Category: category is null ? null : new ExpenseCategoryInfo(category.Id, category.Name, category.Color),
                Source: source,
                IsRecurring: isRecurring,
                Recurrence: recurrence,
                Occurrences: monthDict));
        }

        var totals = months
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<ExpenseCalendarRow>(months, rows, totals);
    }

    private void CapOrDeleteSegment(ExpenseSegment segment, DateOnly boundary)
    {
        if (segment.RecurrenceRule is null)
        {
            expenseRepository.DeleteSegment(segment);
            return;
        }

        var previous = RecurrenceExpander.GetPreviousOccurrence(segment.EffectiveFrom, segment.RecurrenceRule, boundary);
        if (previous is null)
        {
            expenseRepository.DeleteSegment(segment);
        }
        else
        {
            segment.RecurrenceRule.EndDate = previous;
            expenseRepository.UpdateSegment(segment);
        }
    }

    private static RecurrenceRule? MapRecurrence(CreateRecurrenceRequest? request) =>
        request is null
            ? null
            : new RecurrenceRule
            {
                Frequency = request.Frequency,
                Interval = request.Interval,
                EndDate = request.EndDate,
                TotalInstallments = request.TotalInstallments
            };

    private static IReadOnlyDictionary<string, decimal> SumByCurrency(IEnumerable<ExpenseResponse> occurrences)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var occurrence in occurrences)
        {
            foreach (var (currency, amount) in occurrence.Amounts)
            {
                totals[currency] = totals.GetValueOrDefault(currency) + amount;
            }
        }
        return totals;
    }

    private static ExpenseDetailResponse MapDetail(ExpenseSeries series) =>
        new(
            Id: series.Id,
            UserId: series.UserId,
            Description: series.Description,
            CategoryId: series.CategoryId,
            Category: series.Category,
            Segments: series.Segments
                .OrderBy(s => s.EffectiveFrom)
                .Select(s => new ExpenseSegmentResponse(
                    Id: s.Id,
                    EffectiveFrom: s.EffectiveFrom,
                    Amount: s.Amount,
                    Currency: s.Currency,
                    RecurrenceRule: s.RecurrenceRule,
                    PaycheckSeriesId: s.PaycheckSeriesId,
                    PaycheckSeries: s.PaycheckSeries is null
                        ? null
                        : new PaycheckSummary(s.PaycheckSeries.Id, s.PaycheckSeries.Description),
                    InvoiceSeriesId: s.InvoiceSeriesId,
                    InvoiceSeries: s.InvoiceSeries is null
                        ? null
                        : new InvoiceSummary(s.InvoiceSeries.Id, s.InvoiceSeries.Description, s.InvoiceSeries.Type)))
                .ToList(),
            Exceptions: series.Exceptions
                .OrderBy(e => e.OriginalDate)
                .Select(e => new ExpenseExceptionResponse(
                    Id: e.Id,
                    OriginalDate: e.OriginalDate,
                    Date: e.Date,
                    Amount: e.Amount,
                    Currency: e.Currency,
                    IsDeleted: e.IsDeleted))
                .ToList());

    private static ExpenseResponse MapOccurrence(ExpenseOccurrence occurrence, ExpenseSeries series, CurrencyScope scope)
    {
        var segment = occurrence.Segment;
        var amount = occurrence.Exception?.Amount ?? segment!.Amount;
        var currency = occurrence.Exception?.Currency ?? segment!.Currency;
        var date = occurrence.Date;
        var rule = segment?.RecurrenceRule;
        var hasInstallments = rule?.TotalInstallments is not null;
        var isOverride = occurrence.Exception is not null && occurrence.OriginalDate.HasValue;

        return new ExpenseResponse(
            Id: occurrence.Exception?.Id ?? series.Id,
            Date: date,
            Amount: amount,
            Currency: currency,
            Amounts: scope.ConvertToDisplay(amount, currency, date),
            Description: series.Description,
            CategoryId: series.CategoryId,
            PaycheckSeriesId: segment?.PaycheckSeriesId,
            InvoiceSeriesId: segment?.InvoiceSeriesId,
            IsRecurring: rule is not null,
            RecurringExpenseId: rule is not null ? series.Id : null,
            OriginalDate: isOverride ? occurrence.OriginalDate : null,
            IsOverride: isOverride,
            Recurrence: segment is null || rule is null
                ? null
                : new RecurrenceInfo(
                    segment.EffectiveFrom,
                    rule.Frequency,
                    rule.Interval,
                    rule.EndDate,
                    rule.TotalInstallments),
            InstallmentNumber: hasInstallments ? occurrence.OccurrenceIndex + 1 : null,
            TotalInstallments: rule?.TotalInstallments);
    }
}
