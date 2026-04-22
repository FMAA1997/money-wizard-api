using Domain.Models;

namespace Domain.Requests;

public sealed record UpdatePaycheckRequest(
    DateOnly Date, decimal Amount, string Currency, string Description,
    CreateRecurrenceRequest? Recurrence);
