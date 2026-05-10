using ErrorOr;

namespace Domain.Errors;

public static class PaycheckErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Paycheck.NotFound", "Paycheck not found.");

    public static readonly Error SegmentNotFound = Error.NotFound(
        "Paycheck.SegmentNotFound", "No paycheck segment covers the requested date.");
}
