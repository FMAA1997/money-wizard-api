namespace Domain.Requests;

public sealed record UpdateExpenseRequest(DateOnly Date, decimal Amount, string Description, Guid? CategoryId, Guid? Source);
