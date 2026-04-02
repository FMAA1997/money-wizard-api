using ErrorOr;

namespace Domain.Errors;

public sealed record DomainError(string Code, string Description)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);
}

public static class AuthErrors
{
    public static readonly Error MissingExternalId = Error.Unauthorized(
        "Auth.MissingExternalId", "External identity could not be resolved from the token.");

    public static readonly Error MissingEmail = Error.Unauthorized(
        "Auth.MissingEmail", "Email could not be resolved from the token.");

    public static readonly Error UserNotFound = Error.NotFound(
        "Auth.UserNotFound", "No user found for this account.");

    public static readonly Error UserAlreadyExists = Error.Conflict(
        "Auth.UserAlreadyExists", "A user with this account already exists.");

    public static readonly Error EmailAlreadyInUse = Error.Conflict(
        "Auth.EmailAlreadyInUse", "This email address is already associated with another account.");
}
