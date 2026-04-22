using Application.DTOs.Profile;
using ErrorOr;

namespace Application.Abstractions.Services;

public interface ICountryProfileHandler
{
    string CountryCode { get; }

    IReadOnlyList<string> DisplayCurrencies { get; }

    string PrimaryCurrency { get; }

    Task<ErrorOr<Success>> Upsert(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    Task<object?> LoadResponseSlice(Guid userId, CancellationToken cancellationToken = default);

    Task<ErrorOr<Guid>> GetInvoiceCategoryId(Guid userId, CancellationToken cancellationToken = default);
}
