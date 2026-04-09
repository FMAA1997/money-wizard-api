using Application.DTOs.Expense;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseService
{
    Task<ErrorOr<IReadOnlyList<ExpenseResponse>>> GetAllInRange(string? externalId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Create(string? externalId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Update(string? externalId, Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseResponse>> UpdateOccurrence(string? externalId, Guid id, DateOnly date, UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> UpdateFromDate(string? externalId, Guid id, DateOnly date, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteOccurrence(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteFromDate(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
