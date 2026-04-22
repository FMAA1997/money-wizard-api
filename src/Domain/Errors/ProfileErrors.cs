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

    public static readonly Error InvalidCountryPayload = Error.Validation(
        "Profile.InvalidCountryPayload", "Request payload does not match the specified country.");

    public static readonly Error ArgentinaPayloadRequired = Error.Validation(
        "Profile.ArgentinaPayloadRequired", "Argentina profile payload is required when country is 'ar'.");

    public static readonly Error InvalidName = Error.Validation(
        "Profile.InvalidName", "Name is required.");

    public static readonly Error InvalidDateOfBirth = Error.Validation(
        "Profile.InvalidDateOfBirth", "Date of birth cannot be in the future.");

    public static readonly Error InvoiceCategoryNotConfigured = Error.Validation(
        "Profile.InvoiceCategoryNotConfigured", "No invoice category is configured for your profile.");
}
