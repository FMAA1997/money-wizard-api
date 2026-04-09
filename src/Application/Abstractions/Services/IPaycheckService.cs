using Application.DTOs.Paycheck;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IPaycheckService
{
    Task<ErrorOr<IReadOnlyList<PaycheckResponse>>> GetAllInRange(string? externalId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckCalendarResponse>> GetCalendar(string? externalId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> Create(string? externalId, CreatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> Update(string? externalId, Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<PaycheckResponse>> UpdateOccurrence(string? externalId, Guid id, DateOnly date, UpdatePaycheckOccurrenceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Paycheck>> UpdateFromDate(string? externalId, Guid id, DateOnly date, UpdatePaycheckRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteOccurrence(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteFromDate(string? externalId, Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
