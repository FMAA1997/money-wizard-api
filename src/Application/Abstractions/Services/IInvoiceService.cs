using Application.DTOs.Invoice;
using Application.DTOs.Shared;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface IInvoiceService
{
    Task<ErrorOr<IReadOnlyList<InvoiceDetailResponse>>> GetAllInRange(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<CalendarResponse<InvoiceCalendarRow>>> GetCalendar(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<ErrorOr<InvoiceDetailResponse>> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Invoice>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Invoice>> Update(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> Delete(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<InvoiceResponse>> UpdateOccurrence(Guid id, DateOnly date, UpdateInvoiceOccurrenceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Invoice>> UpdateFromDate(Guid id, DateOnly date, UpdateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteOccurrence(Guid id, DateOnly date, CancellationToken cancellationToken = default);
    Task<ErrorOr<Deleted>> DeleteFromDate(Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
