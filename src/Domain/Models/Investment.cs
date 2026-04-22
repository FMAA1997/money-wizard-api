using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class Investment : Entity
{
    public Guid UserId { get; set; }
    public AssetClass AssetClass { get; set; }
    public string? Ticker { get; set; }
    public required string Description { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly Date { get; set; }
    public decimal? ManualYield { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
}
