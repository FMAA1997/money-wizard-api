using Application.DTOs.Expense;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IExpenseService
{
    Task<ErrorOr<IReadOnlyList<Expense>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<CalendarResponse<ExpenseCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Create(CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> Update(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<ExpenseResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdateExpenseOccurrenceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Expense>> UpdateFromDate(Guid id, DateOnly date, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
