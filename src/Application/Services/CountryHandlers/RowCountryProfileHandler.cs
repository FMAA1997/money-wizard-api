using Application.Abstractions.Services;
using Application.DTOs.Profile;
using Domain.Errors;
using ErrorOr;

namespace Application.Services.CountryHandlers;

public sealed class RowCountryProfileHandler : ICountryProfileHandler
{
    private static readonly IReadOnlyList<string> Currencies = new[] { "USD" };

    public string CountryCode => "row";

    public IReadOnlyList<string> DisplayCurrencies => Currencies;

    public string PrimaryCurrency => "USD";

    public Task<ErrorOr<Success>> Upsert(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public Task<object?> LoadResponseSlice(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);

    public Task<ErrorOr<Guid>> GetInvoiceCategoryId(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorOr<Guid>>(ProfileErrors.InvoiceCategoryNotConfigured);
}
