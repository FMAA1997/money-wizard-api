using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Expense;
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
    public async Task<ErrorOr<IReadOnlyList<ExpenseDetailResponse>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);
        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        return expenses.Select(e => MapEntityDetail(e, scope)).ToList();
    }

    public async Task<ErrorOr<CalendarResponse<ExpenseCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);

        var occurrences = await ExpandOccurrences(expenses, startDate, endDate, cancellationToken);

        var expenseLookup = expenses.ToDictionary(e => e.Id);

        var months = new List<string>();
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);
        while (current <= end)
        {
            months.Add(current.ToString("yyyy-MM"));
            current = current.AddMonths(1);
        }

        var rows = occurrences
            .GroupBy(o => o.RecurringExpenseId ?? o.Id)
            .Select(g =>
            {
                var first = g.First();
                var seriesEntity = expenseLookup.GetValueOrDefault(g.Key);
                var monthDict = g
                    .GroupBy(o => o.Date.ToString("yyyy-MM"))
                    .ToDictionary(
                        mg => mg.Key,
                        mg => (IReadOnlyList<ExpenseResponse>)[.. mg.OrderBy(o => o.Date)]);

                var category = seriesEntity?.Category;
                var source = seriesEntity?.Paycheck is not null
                    ? new ExpenseSourceInfo(seriesEntity.Paycheck.Id, seriesEntity.Paycheck.Description, seriesEntity.Paycheck.Amount, "Paycheck")
                    : seriesEntity?.Invoice is not null
                    ? new ExpenseSourceInfo(seriesEntity.Invoice.Id, seriesEntity.Invoice.Description, seriesEntity.Invoice.Amount, "Invoice")
                    : null;

                return new ExpenseCalendarRow(
                    ExpenseId: g.Key,
                    Description: first.Description,
                    Category: category is null ? null : new ExpenseCategoryInfo(category.Id, category.Name, category.Color),
                    Source: source,
                    IsRecurring: first.IsRecurring,
                    Recurrence: first.Recurrence,
                    Occurrences: monthDict);
            })
            .ToList();

        var totals = months
            .Select(m => SumByCurrency(rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])))
            .ToList();

        return new CalendarResponse<ExpenseCalendarRow>(months, rows, totals);
    }

    public async Task<ErrorOr<ExpenseDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        return MapEntityDetail(expense, scope);
    }

    public async Task<ErrorOr<Expense>> Create(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var expense = new Expense
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            CategoryId = request.CategoryId,
            PaycheckId = request.PaycheckId,
            InvoiceId = request.InvoiceId,
            RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
            {
                Frequency = request.Recurrence.Frequency,
                Interval = request.Recurrence.Interval,
                EndDate = request.Recurrence.EndDate,
                TotalInstallments = request.Recurrence.TotalInstallments
            }
        };

        await expenseRepository.Add(expense, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return expense;
    }

    public async Task<ErrorOr<Expense>> Update(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        expense.Date = request.Date;
        expense.Amount = request.Amount;
        expense.Currency = request.Currency;
        expense.Description = request.Description;
        expense.CategoryId = request.CategoryId;
        expense.PaycheckId = request.PaycheckId;
        expense.InvoiceId = request.InvoiceId;
        expense.RecurrenceRule = request.Recurrence is null ? null : new RecurrenceRule
        {
            Frequency = request.Recurrence.Frequency,
            Interval = request.Recurrence.Interval,
            EndDate = request.Recurrence.EndDate,
            TotalInstallments = request.Recurrence.TotalInstallments
        };

        expenseRepository.Update(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return expense;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        expenseRepository.Delete(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<ExpenseResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await expenseRepository.GetException(id, date, cancellationToken);
        var occurrenceIndex = RecurrenceExpander.GetOccurrenceIndex(series.Date, series.RecurrenceRule, date);

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Date = request.Date ?? date;
            existing.Amount = request.Amount ?? series.Amount;
            existing.Currency = request.Currency ?? series.Currency;
            existing.Description = request.Description ?? series.Description;
            existing.CategoryId = request.CategoryId ?? series.CategoryId;
            existing.PaycheckId = request.PaycheckId ?? series.PaycheckId;
            existing.InvoiceId = request.InvoiceId ?? series.InvoiceId;
            existing.IsDeleted = false;

            expenseRepository.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapOverride(existing, series, occurrenceIndex, scope);
        }

        var exception = new Expense
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date ?? date,
            Amount = request.Amount ?? series.Amount,
            Currency = request.Currency ?? series.Currency,
            Description = request.Description ?? series.Description,
            CategoryId = request.CategoryId ?? series.CategoryId,
            PaycheckId = request.PaycheckId ?? series.PaycheckId,
            InvoiceId = request.InvoiceId ?? series.InvoiceId,
            RecurringExpenseId = id,
            OriginalDate = date
        };

        await expenseRepository.Add(exception, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapOverride(exception, series, occurrenceIndex, scope);
    }

    public async Task<ErrorOr<Expense>> UpdateFromDate(Guid id, DateOnly date, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        if (request.Recurrence is not null && request.Recurrence.Interval < 1)
            return RecurrenceErrors.InvalidInterval;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);
        series.RecurrenceRule.EndDate = previousDate;

        if (previousDate is null)
        {
            expenseRepository.Delete(series);
        }
        else
        {
            expenseRepository.Update(series);
        }

        await expenseRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        var recurrence = request.Recurrence;
        var newSeries = new Expense
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            CategoryId = request.CategoryId,
            PaycheckId = request.PaycheckId,
            InvoiceId = request.InvoiceId,
            RecurrenceRule = recurrence is null ? null : new RecurrenceRule
            {
                Frequency = recurrence.Frequency,
                Interval = recurrence.Interval,
                EndDate = recurrence.EndDate,
                TotalInstallments = recurrence.TotalInstallments
            }
        };

        await expenseRepository.Add(newSeries, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return newSeries;
    }

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var existing = await expenseRepository.GetException(id, date, cancellationToken);

        if (existing is not null)
        {
            existing.IsDeleted = true;
            expenseRepository.Update(existing);
        }
        else
        {
            var exception = new Expense
            {
                UserId = currentUserProvider.UserId,
                Date = date,
                Amount = series.Amount,
                Currency = series.Currency,
                Description = series.Description,
                RecurringExpenseId = id,
                OriginalDate = date,
                IsDeleted = true
            };
            await expenseRepository.Add(exception, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;
        if (series.RecurrenceRule is null)
            return RecurrenceErrors.NotRecurring;
        if (!RecurrenceExpander.IsValidOccurrence(series.Date, series.RecurrenceRule, date))
            return RecurrenceErrors.InvalidOccurrenceDate;

        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);

        await expenseRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        if (previousDate is null)
        {
            expenseRepository.Delete(series);
        }
        else
        {
            series.RecurrenceRule.EndDate = previousDate;
            expenseRepository.Update(series);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }

    private async Task<List<ExpenseResponse>> ExpandOccurrences(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var oneOffs = new List<Expense>();
        var series = new List<Expense>();
        var exceptionLookup = new Dictionary<(Guid, DateOnly), Expense>();

        foreach (var e in expenses)
        {
            if (e.RecurrenceRule is not null)
                series.Add(e);
            else if (e.RecurringExpenseId.HasValue && e.OriginalDate.HasValue)
                exceptionLookup[(e.RecurringExpenseId.Value, e.OriginalDate.Value)] = e;
            else
                oneOffs.Add(e);
        }

        var scope = await currencyConverter.OpenScopeAsync(cancellationToken);
        var results = new List<ExpenseResponse>();

        foreach (var oneOff in oneOffs)
        {
            results.Add(MapOneOff(oneOff, scope));
        }

        foreach (var s in series)
        {
            var occurrences = RecurrenceExpander.Expand(s.Date, s.RecurrenceRule!, startDate, endDate);

            foreach (var (date, index) in occurrences)
            {
                var key = (s.Id, date);
                if (exceptionLookup.TryGetValue(key, out var exception))
                {
                    if (exception.IsDeleted)
                        continue;

                    results.Add(MapOverride(exception, s, index, scope));
                }
                else
                {
                    results.Add(MapVirtual(s, date, index, scope));
                }
            }
        }

        results.Sort((a, b) => a.Date.CompareTo(b.Date));
        return results;
    }

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

    private static ExpenseDetailResponse MapEntityDetail(Expense expense, CurrencyScope scope) =>
        new(
            Id: expense.Id,
            UserId: expense.UserId,
            Date: expense.Date,
            Amount: expense.Amount,
            Currency: expense.Currency,
            Description: expense.Description,
            CategoryId: expense.CategoryId,
            PaycheckId: expense.PaycheckId,
            InvoiceId: expense.InvoiceId,
            RecurrenceRule: expense.RecurrenceRule,
            RecurringExpenseId: expense.RecurringExpenseId,
            OriginalDate: expense.OriginalDate,
            IsDeleted: expense.IsDeleted,
            Category: expense.Category,
            Paycheck: expense.Paycheck,
            Invoice: expense.Invoice,
            Amounts: scope.ConvertToDisplay(expense.Amount, expense.Currency, expense.Date));

    private static ExpenseResponse MapOneOff(Expense expense, CurrencyScope scope) =>
        new(
            Id: expense.Id,
            Date: expense.Date,
            Amount: expense.Amount,
            Currency: expense.Currency,
            Amounts: scope.ConvertToDisplay(expense.Amount, expense.Currency, expense.Date),
            Description: expense.Description,
            CategoryId: expense.CategoryId,
            PaycheckId: expense.PaycheckId,
            InvoiceId: expense.InvoiceId,
            IsRecurring: false,
            RecurringExpenseId: null,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: null,
            InstallmentNumber: null,
            TotalInstallments: null);

    private static ExpenseResponse MapVirtual(Expense series, DateOnly date, int occurrenceIndex, CurrencyScope scope) =>
        new(
            Id: series.Id,
            Date: date,
            Amount: series.Amount,
            Currency: series.Currency,
            Amounts: scope.ConvertToDisplay(series.Amount, series.Currency, date),
            Description: series.Description,
            CategoryId: series.CategoryId,
            PaycheckId: series.PaycheckId,
            InvoiceId: series.InvoiceId,
            IsRecurring: true,
            RecurringExpenseId: series.Id,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: new RecurrenceInfo(
                series.Date,
                series.RecurrenceRule!.Frequency,
                series.RecurrenceRule.Interval,
                series.RecurrenceRule.EndDate,
                series.RecurrenceRule.TotalInstallments),
            InstallmentNumber: series.RecurrenceRule.TotalInstallments.HasValue ? occurrenceIndex + 1 : null,
            TotalInstallments: series.RecurrenceRule.TotalInstallments);

    private static ExpenseResponse MapOverride(Expense exception, Expense series, int occurrenceIndex, CurrencyScope scope) =>
        new(
            Id: exception.Id,
            Date: exception.Date,
            Amount: exception.Amount,
            Currency: exception.Currency,
            Amounts: scope.ConvertToDisplay(exception.Amount, exception.Currency, exception.Date),
            Description: exception.Description,
            CategoryId: exception.CategoryId,
            PaycheckId: exception.PaycheckId,
            InvoiceId: exception.InvoiceId,
            IsRecurring: true,
            RecurringExpenseId: exception.RecurringExpenseId,
            OriginalDate: exception.OriginalDate,
            IsOverride: true,
            Recurrence: new RecurrenceInfo(
                series.Date,
                series.RecurrenceRule!.Frequency,
                series.RecurrenceRule.Interval,
                series.RecurrenceRule.EndDate,
                series.RecurrenceRule.TotalInstallments),
            InstallmentNumber: series.RecurrenceRule.TotalInstallments.HasValue ? occurrenceIndex + 1 : null,
            TotalInstallments: series.RecurrenceRule.TotalInstallments);
}
