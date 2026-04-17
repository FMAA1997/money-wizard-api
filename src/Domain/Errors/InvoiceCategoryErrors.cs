using ErrorOr;

namespace Domain.Errors;

public static class InvoiceCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "InvoiceCategory.NotFound", "Invoice category not found.");
}
