namespace Application.DTOs.Expense.Statistics;

public class SpendingByCategory
{
    public List<CategoryInfo> Categories { get; set; } = [];
    public List<Dictionary<string, object>> Data { get; set; } = [];
}

public class CategoryInfo
{
    public required string Name { get; set; }
    public required string Color { get; set; }
}
