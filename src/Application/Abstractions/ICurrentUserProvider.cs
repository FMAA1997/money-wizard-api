namespace Application.Abstractions;

public interface ICurrentUserProvider
{
    Guid UserId { get; }
}
