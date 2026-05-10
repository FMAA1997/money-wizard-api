using ErrorOr;

namespace Domain.Errors;

public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Invoice.NotFound", "Invoice not found.");

    public static readonly Error SegmentNotFound = Error.NotFound(
        "Invoice.SegmentNotFound", "No invoice segment covers the requested date.");

    public static readonly Error ParentRequired = Error.Validation(
        "Invoice.ParentRequired", "A credit or debit note must reference a parent invoice.");

    public static readonly Error ParentNotAllowed = Error.Validation(
        "Invoice.ParentNotAllowed", "An invoice cannot reference a parent invoice.");

    public static readonly Error ParentNotFound = Error.Validation(
        "Invoice.ParentNotFound", "Parent invoice not found.");

    public static readonly Error ParentMustBeInvoice = Error.Validation(
        "Invoice.ParentMustBeInvoice", "Parent must be an invoice, not another credit or debit note.");

    public static readonly Error ParentOccurrenceNotValid = Error.Validation(
        "Invoice.ParentOccurrenceNotValid",
        "The referenced parent occurrence is not a valid issuance of the parent invoice series.");
}
