using Application.Abstractions.Services;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Services;

public sealed class ExpenseService(
    IExpenseRepository expenseRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IExpenseService
{
    public async Task<ErrorOr<IReadOnlyList<Expense>>> GetAll(string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var expenses = await expenseRepository.GetByUserId(user.Id, cancellationToken);
        return expenses.ToList();
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

        var expense = new Expense
        {
            UserId = user.Id,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Source = request.Source
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

        expense.Date = request.Date;
        expense.Amount = request.Amount;
        expense.Description = request.Description;
        expense.CategoryId = request.CategoryId;
        expense.Source = request.Source;

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
}
