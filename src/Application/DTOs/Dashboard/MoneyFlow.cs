namespace Application.DTOs.Dashboard;

public class MoneyFlow
{
    public List<MoneyFlowNode> Nodes { get; set; } = [];
    public List<MoneyFlowLink> Links { get; set; } = [];
}

public class MoneyFlowNode
{
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public string? Color { get; set; }
}

public class MoneyFlowLink
{
    public int Source { get; set; }
    public int Target { get; set; }
    public decimal Value { get; set; }
}
