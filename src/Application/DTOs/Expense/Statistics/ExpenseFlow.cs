namespace Application.DTOs.Expense.Statistics;

public class ExpenseFlow
{
    public List<ExpenseFlowNode> Nodes { get; set; } = [];
    public List<ExpenseFlowLink> Links { get; set; } = [];
}

public class ExpenseFlowNode
{
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public string? Color { get; set; }
}

public class ExpenseFlowLink
{
    public int Source { get; set; }
    public int Target { get; set; }
    public required IReadOnlyDictionary<string, decimal> Value { get; set; }
}
