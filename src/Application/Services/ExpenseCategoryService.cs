using Application.Abstractions;
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
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork) : IExpenseCategoryService
{
    public async Task<ErrorOr<IReadOnlyList<ExpenseCategory>>> GetAll(CancellationToken cancellationToken = default)
    {
        var categories = await expenseCategoryRepository.GetByUserId(currentUserProvider.UserId, cancellationToken);
        return categories.ToList();
    }

    public async Task<ErrorOr<ExpenseCategory>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != currentUserProvider.UserId)
            return ExpenseCategoryErrors.NotFound;

        return category;
    }

    public async Task<ErrorOr<ExpenseCategory>> Create(CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = new ExpenseCategory
        {
            UserId = currentUserProvider.UserId,
            Name = request.Name,
            Color = request.Color
        };

        await expenseCategoryRepository.Add(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<ErrorOr<ExpenseCategory>> Update(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != currentUserProvider.UserId)
            return ExpenseCategoryErrors.NotFound;

        category.Name = request.Name;
        category.Color = request.Color;

        expenseCategoryRepository.Update(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await expenseCategoryRepository.GetById(id, cancellationToken);
        if (category is null || category.UserId != currentUserProvider.UserId)
            return ExpenseCategoryErrors.NotFound;

        expenseCategoryRepository.Delete(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
