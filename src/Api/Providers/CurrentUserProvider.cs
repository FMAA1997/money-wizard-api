using Application.Abstractions;

namespace Api.Providers;

public sealed class CurrentUserProvider : ICurrentUserProvider
{
    public Guid UserId { get; set; }
}
