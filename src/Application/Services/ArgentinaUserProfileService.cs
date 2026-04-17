using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Profile;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using ErrorOr;

namespace Application.Services;

public sealed class ArgentinaUserProfileService(
    IArgentinaUserProfileRepository profileRepository,
    IInvoiceCategoryRepository invoiceCategoryRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork unitOfWork) : IArgentinaUserProfileService
{
    private static readonly HashSet<string> ValidEmploymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "monotributo",
        "other"
    };

    public async Task<ErrorOr<ArgentinaProfileResponse>> GetForCurrentUser(CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserId(currentUserProvider.UserId, cancellationToken);
        if (profile is null)
            return ProfileErrors.ArgentinaProfileNotFound;

        return Map(profile);
    }

    public async Task<ErrorOr<ArgentinaProfileResponse>> Upsert(UpsertArgentinaProfileRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await Validate(request, cancellationToken);
        if (validation.IsError)
            return validation.Errors;

        var employment = request.EmploymentStatus.ToLowerInvariant();
        var categoryId = employment == "monotributo" ? request.MonotributoCategoryId : null;

        var profile = await profileRepository.GetByUserId(currentUserProvider.UserId, cancellationToken);

        if (profile is null)
        {
            profile = new ArgentinaUserProfile
            {
                UserId = currentUserProvider.UserId,
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(profile);
    }

    private async Task<ErrorOr<Success>> Validate(UpsertArgentinaProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmploymentStatus) ||
            !ValidEmploymentStatuses.Contains(request.EmploymentStatus))
            return ProfileErrors.InvalidEmploymentStatus;

        var employment = request.EmploymentStatus.ToLowerInvariant();

        if (employment == "monotributo")
        {
            if (!request.MonotributoCategoryId.HasValue)
                return ProfileErrors.MonotributoCategoryRequired;

            var category = await invoiceCategoryRepository.GetById(
                request.MonotributoCategoryId.Value, cancellationToken);
            if (category is null)
                return ProfileErrors.MonotributoCategoryNotFound;
        }
        else if (request.MonotributoCategoryId.HasValue)
        {
            return ProfileErrors.MonotributoCategoryNotAllowed;
        }

        return Result.Success;
    }

    public static ArgentinaProfileResponse Map(ArgentinaUserProfile profile)
        => new(profile.Id, profile.UserId, profile.EmploymentStatus, profile.MonotributoCategoryId);
}
