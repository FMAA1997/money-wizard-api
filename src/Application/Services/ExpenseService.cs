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
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IExpenseService
{
    public async Task<ErrorOr<IReadOnlyList<ExpenseResponse>>> GetAllInRange(string? externalId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;
        if (startDate > endDate)
            return RecurrenceErrors.InvalidDateRange;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var expenses = await expenseRepository.GetByUserIdInRange(user.Id, startDate, endDate, cancellationToken);

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

    public async Task<ErrorOr<Expense>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != user.Id)
            return ExpenseErrors.NotFound;

        return expense;
    }

    public async Task<ErrorOr<Expense>> Create(string? externalId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        if (request.Recurrence is not null)
        {
            if (request.Recurrence.Interval < 1)
                return RecurrenceErrors.InvalidInterval;
            if (request.Recurrence.EndDate.HasValue && request.Recurrence.EndDate.Value < request.Date)
                return RecurrenceErrors.EndDateBeforeStart;
        }

        var expense = new Expense
        {
            UserId = user.Id,
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

    public async Task<ErrorOr<Expense>> Update(string? externalId, Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != user.Id)
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

    public async Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var expense = await expenseRepository.GetById(id, cancellationToken);
        if (expense is null || expense.UserId != user.Id)
            return ExpenseErrors.NotFound;

        expenseRepository.Delete(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }

    public async Task<ErrorOr<ExpenseResponse>> UpdateOccurrence(string? externalId, Guid id, DateOnly date, UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != user.Id)
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
            UserId = user.Id,
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

    public async Task<ErrorOr<Expense>> UpdateFromDate(string? externalId, Guid id, DateOnly date, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != user.Id)
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
            UserId = user.Id,
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

    public async Task<ErrorOr<Deleted>> DeleteOccurrence(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != user.Id)
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
                UserId = user.Id,
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

    public async Task<ErrorOr<Deleted>> DeleteFromDate(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var series = await expenseRepository.GetById(id, cancellationToken);
        if (series is null || series.UserId != user.Id)
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
