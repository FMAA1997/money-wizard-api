using ErrorOr;

namespace Domain.Errors;

public static class PaycheckErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Paycheck.NotFound", "Paycheck not found.");
}
