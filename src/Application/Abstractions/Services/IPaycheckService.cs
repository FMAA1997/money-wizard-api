using Application.DTOs.Paycheck;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IPaycheckService
{
    Task<ErrorOr<IReadOnlyList<PaycheckDetailResponse>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<CalendarResponse<PaycheckCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckSeries>> Create(CreatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckSeries>> Update(Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckSeries>> UpdateFromDate(Guid id, DateOnly date, UpdatePaycheckFromDateRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
