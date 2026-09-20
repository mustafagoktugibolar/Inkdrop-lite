namespace Inkdrop_lite.Domain.Common;

public abstract class UserOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OwnerId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
