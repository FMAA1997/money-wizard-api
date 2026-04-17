using ErrorOr;

namespace Domain.Errors;

public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Invoice.NotFound", "Invoice not found.");
}
