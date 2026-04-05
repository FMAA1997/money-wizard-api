using Application.Abstractions.Services;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Services;

public sealed class ExpenseCategoryService(
    IExpenseCategoryRepository expenseCategoryRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IExpenseCategoryService
{
    public async Task<ErrorOr<IReadOnlyList<ExpenseCategory>>> GetAll(string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var categories = await expenseCategoryRepository.GetByUserId(user.Id, cancellationToken);
        return categories.ToList();
    }

    public async Task<ErrorOr<ExpenseCategory>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != user.Id)
            return ExpenseCategoryErrors.NotFound;

        return category;
    }

    public async Task<ErrorOr<ExpenseCategory>> Create(string? externalId, CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var category = new ExpenseCategory
        {
            UserId = user.Id,
            Name = request.Name,
            Color = request.Color
        };

        await expenseCategoryRepository.Add(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<ErrorOr<ExpenseCategory>> Update(string? externalId, Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != user.Id)
            return ExpenseCategoryErrors.NotFound;

        category.Name = request.Name;
        category.Color = request.Color;

        expenseCategoryRepository.Update(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != user.Id)
            return ExpenseCategoryErrors.NotFound;

        expenseCategoryRepository.Delete(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
