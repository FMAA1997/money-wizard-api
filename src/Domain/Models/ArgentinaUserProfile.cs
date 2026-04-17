using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class ArgentinaUserProfile : Entity
{
    public Guid UserId { get; set; }
    public required string EmploymentStatus { get; set; }
    public Guid? MonotributoCategoryId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
    [JsonIgnore]
    public InvoiceCategory? MonotributoCategory { get; set; }
}
