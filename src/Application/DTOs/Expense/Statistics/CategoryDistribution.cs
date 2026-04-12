namespace Application.DTOs.Expense.Statistics;

public class CategoryDistribution
{
    public List<CategoryDistributionEntry> Data { get; set; } = [];
}

public class CategoryDistributionEntry
{
    public required string Name { get; set; }
    public decimal Value { get; set; }
    public required string Fill { get; set; }
}
