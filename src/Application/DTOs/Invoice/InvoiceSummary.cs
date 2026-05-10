using Domain.Models;

namespace Application.DTOs.Invoice;

public sealed record InvoiceSummary(Guid Id, string Description, InvoiceType Type);
