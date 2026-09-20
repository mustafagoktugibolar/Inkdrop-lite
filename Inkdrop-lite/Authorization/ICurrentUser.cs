using Inkdrop_lite.Domain.Common;

namespace Inkdrop_lite.Authorization;

public interface ICurrentUser
{
    string? UserId { get; }

    /// <summary>The client making the change; see <c>ChangeSources</c>.</summary>
    string ChangeSource => ChangeSources.App;
}
