using ErrorOr;

namespace Domain.Errors;

public static class ProfileErrors
{
    public static readonly Error InvalidCountry = Error.Validation(
        "Profile.InvalidCountry", "Unsupported country code.");

    public static readonly Error InvalidEmploymentStatus = Error.Validation(
        "Profile.InvalidEmploymentStatus", "Employment status must be 'monotributo' or 'other'.");

    public static readonly Error MonotributoCategoryNotFound = Error.Validation(
        "Profile.MonotributoCategoryNotFound", "The selected monotributo category does not exist.");

    public static readonly Error MonotributoCategoryRequired = Error.Validation(
        "Profile.MonotributoCategoryRequired", "Monotributo category is required when employment status is 'monotributo'.");

    public static readonly Error MonotributoCategoryNotAllowed = Error.Validation(
        "Profile.MonotributoCategoryNotAllowed", "Monotributo category is only allowed when employment status is 'monotributo'.");

    public static readonly Error ArgentinaProfileNotFound = Error.NotFound(
        "Profile.ArgentinaProfileNotFound", "No Argentina profile exists for this user.");
}
