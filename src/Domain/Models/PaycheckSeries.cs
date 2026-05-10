using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class PaycheckSeries : Entity
{
    public Guid UserId { get; set; }
    public required string Description { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    public ICollection<PaycheckSegment> Segments { get; set; } = [];
    public ICollection<PaycheckException> Exceptions { get; set; } = [];
}
