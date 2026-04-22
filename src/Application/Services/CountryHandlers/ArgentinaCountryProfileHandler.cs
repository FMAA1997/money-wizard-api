using Application.Abstractions.Services;
using Application.DTOs.Profile;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services.CountryHandlers;

public sealed class ArgentinaCountryProfileHandler(
    IArgentinaUserProfileRepository profileRepository,
    IInvoiceCategoryRepository invoiceCategoryRepository) : ICountryProfileHandler
{
    private static readonly HashSet<string> ValidEmploymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "monotributo",
        "other"
    };

    private static readonly IReadOnlyList<string> Currencies = new[] { "ARS", "USD" };

    public string CountryCode => "ar";

    public IReadOnlyList<string> DisplayCurrencies => Currencies;

    public string PrimaryCurrency => "USD";

    public async Task<ErrorOr<Success>> Upsert(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Argentina is null)
            return ProfileErrors.ArgentinaPayloadRequired;

        var payload = request.Argentina;

        var validation = await Validate(payload, cancellationToken);
        if (validation.IsError)
            return validation.Errors;

        var employment = payload.EmploymentStatus.ToLowerInvariant();
        var categoryId = employment == "monotributo" ? payload.MonotributoCategoryId : null;

        var profile = await profileRepository.GetByUserId(userId, cancellationToken);

        if (profile is null)
        {
            profile = new ArgentinaUserProfile
            {
                UserId = userId,
                EmploymentStatus = employment,
                MonotributoCategoryId = categoryId
            };
            await profileRepository.Add(profile, cancellationToken);
        }
        else
        {
            profile.EmploymentStatus = employment;
            profile.MonotributoCategoryId = categoryId;
            profileRepository.Update(profile);
        }

        return Result.Success;
    }

    public async Task<object?> LoadResponseSlice(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserId(userId, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<ErrorOr<Guid>> GetInvoiceCategoryId(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserId(userId, cancellationToken);
        if (profile?.MonotributoCategoryId is null)
            return ProfileErrors.InvoiceCategoryNotConfigured;

        return profile.MonotributoCategoryId.Value;
    }

    private async Task<ErrorOr<Success>> Validate(ArgentinaProfilePayload payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.EmploymentStatus) ||
            !ValidEmploymentStatuses.Contains(payload.EmploymentStatus))
            return ProfileErrors.InvalidEmploymentStatus;

        var employment = payload.EmploymentStatus.ToLowerInvariant();

        if (employment == "monotributo")
        {
            if (!payload.MonotributoCategoryId.HasValue)
                return ProfileErrors.MonotributoCategoryRequired;

            var category = await invoiceCategoryRepository.GetById(
                payload.MonotributoCategoryId.Value, cancellationToken);
            if (category is null)
                return ProfileErrors.MonotributoCategoryNotFound;
        }
        else if (payload.MonotributoCategoryId.HasValue)
        {
            return ProfileErrors.MonotributoCategoryNotAllowed;
        }

        return Result.Success;
    }

    private static ArgentinaProfileResponse Map(ArgentinaUserProfile profile)
        => new(profile.Id, profile.UserId, profile.EmploymentStatus, profile.MonotributoCategoryId);
}
