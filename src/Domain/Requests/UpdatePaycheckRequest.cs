using Domain.Models;

namespace Domain.Requests;

public sealed record UpdatePaycheckRequest(
    DateOnly Date, decimal Amount, string Description,
    CreateRecurrenceRequest? Recurrence);
