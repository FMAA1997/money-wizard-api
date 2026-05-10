using Domain.Models;

namespace Domain.Requests;

public sealed record UpdateInvoiceRequest(
    string Description,
    InvoiceClass? Class,
    int? PointOfSale,
    long? BaseNumber);
