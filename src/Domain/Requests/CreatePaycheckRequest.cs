using Domain.Models;

namespace Domain.Requests;

public sealed record CreatePaycheckRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    CreateRecurrenceRequest? Recurrence);
