namespace Infrastructure.Clients;

public sealed class ClientSettings
{
    public string Name { get; init; } = string.Empty;
    public string BaseAddress { get; init; } = string.Empty;
    public Dictionary<string, string> Header { get; init; } = new();
}
