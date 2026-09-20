namespace Inkdrop_lite.Domain.Common;

public abstract class UpdatableUserOwnedEntity : UserOwnedEntity
{
    public DateTime UpdatedAt { get; set; }
}
