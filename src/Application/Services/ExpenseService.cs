using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Expense;
using Application.DTOs.Shared;
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
    IUnitOfWork unitOfWork) : IExpenseService
{
    public async Task<ErrorOr<IReadOnlyList<Expense>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);
        return expenses.ToList();
    }

    public async Task<ErrorOr<CalendarResponse<ExpenseCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var expenses = await expenseRepository.GetByUserIdInRange(currentUserProvider.UserId, startDate, endDate, cancellationToken);

        var occurrences = ExpandOccurrences(expenses, startDate, endDate);

        // Build lookup from expense entities for category/source navigation properties
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
                var source = seriesEntity?.Paycheck;

                return new ExpenseCalendarRow(
                    ExpenseId: g.Key,
                    Description: first.Description,
                    Category: category is null ? null : new ExpenseCategoryInfo(category.Id, category.Name, category.Color),
                    Source: source is null ? null : new ExpenseSourceInfo(source.Id, source.Description, source.Amount),
                    IsRecurring: first.IsRecurring,
                    Recurrence: first.Recurrence,
                    Occurrences: monthDict);
            })
            .ToList();

        var totals = months
            .Select(m => rows
                .Where(r => r.Occurrences.ContainsKey(m))
                .SelectMany(r => r.Occurrences[m])
                .Sum(o => o.Amount))
            .ToList();

        return new CalendarResponse<ExpenseCalendarRow>(months, rows, totals);
    }

    public async Task<ErrorOr<Expense>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != currentUserProvider.UserId)
            return ExpenseErrors.NotFound;

        return expense;
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
            Description = request.Description,
            CategoryId = request.CategoryId,
            Source = request.Source,
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
        expense.Description = request.Description;
        expense.CategoryId = request.CategoryId;
        expense.Source = request.Source;
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

        if (existing is not null)
        {
            existing.Date = request.Date ?? date;
            existing.Amount = request.Amount ?? series.Amount;
            existing.Description = request.Description ?? series.Description;
            existing.CategoryId = request.CategoryId ?? series.CategoryId;
            existing.Source = request.Source ?? series.Source;
            existing.IsDeleted = false;

            expenseRepository.Update(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapOverride(existing, series, occurrenceIndex);
        }

        var exception = new Expense
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date ?? date,
            Amount = request.Amount ?? series.Amount,
            Description = request.Description ?? series.Description,
            CategoryId = request.CategoryId ?? series.CategoryId,
            Source = request.Source ?? series.Source,
            RecurringExpenseId = id,
            OriginalDate = date
        };

        await expenseRepository.Add(exception, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapOverride(exception, series, occurrenceIndex);
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

        // End the old series before the split date
        var previousDate = RecurrenceExpander.GetPreviousOccurrence(series.Date, series.RecurrenceRule, date);
        series.RecurrenceRule.EndDate = previousDate;

        // If no previous occurrence exists, the series starts at or after the split date — delete entirely
        if (previousDate is null)
        {
            expenseRepository.Delete(series);
        }
        else
        {
            expenseRepository.Update(series);
        }

        // Re-parent exceptions from the split date forward to the new series
        await expenseRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        // Create the new series
        var recurrence = request.Recurrence;
        var newSeries = new Expense
        {
            UserId = currentUserProvider.UserId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Source = request.Source,
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

        // Delete future exceptions
        await expenseRepository.DeleteExceptionsFromDate(id, date, cancellationToken);

        if (previousDate is null)
        {
            // Split date is the first occurrence — delete entire series
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

    private static List<ExpenseResponse> ExpandOccurrences(IReadOnlyList<Expense> expenses, DateOnly startDate, DateOnly endDate)
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

        var results = oneOffs
            .Select(MapOneOff)
            .ToList();

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

                    results.Add(MapOverride(exception, s, index));
                }
                else
                {
                    results.Add(MapVirtual(s, date, index));
                }
            }
        }

        results.Sort((a, b) => a.Date.CompareTo(b.Date));
        return results;
    }

    private static ExpenseResponse MapOneOff(Expense expense) =>
        new(
            Id: expense.Id,
            Date: expense.Date,
            Amount: expense.Amount,
            Description: expense.Description,
            CategoryId: expense.CategoryId,
            Source: expense.Source,
            IsRecurring: false,
            RecurringExpenseId: null,
            OriginalDate: null,
            IsOverride: false,
            Recurrence: null,
            InstallmentNumber: null,
            TotalInstallments: null);

    private static ExpenseResponse MapVirtual(Expense series, DateOnly date, int occurrenceIndex) =>
        new(
            Id: series.Id,
            Date: date,
            Amount: series.Amount,
            Description: series.Description,
            CategoryId: series.CategoryId,
            Source: series.Source,
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

    private static ExpenseResponse MapOverride(Expense exception, Expense series, int occurrenceIndex) =>
        new(
            Id: exception.Id,
            Date: exception.Date,
            Amount: exception.Amount,
            Description: exception.Description,
            CategoryId: exception.CategoryId,
            Source: exception.Source,
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
