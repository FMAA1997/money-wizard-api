namespace Domain.Requests;

public sealed record CreateExpenseRequest(DateOnly Date, decimal Amount, string Description, Guid? CategoryId, Guid? Source);
