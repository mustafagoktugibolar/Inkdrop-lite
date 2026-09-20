using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notebooks.Contracts;

public sealed record CreateNotebookRequest
{
    public CreateNotebookRequest(
        string name,
        Guid? parentNotebookId = null,
        int? order = null,
        NotebookIconType iconType = NotebookIconType.None,
        string? iconSvg = null,
        Guid? iconAttachmentId = null)
    {
        Name = name;
        ParentNotebookId = parentNotebookId;
        Order = order;
        IconType = iconType;
        IconSvg = iconSvg;
        IconAttachmentId = iconAttachmentId;
    }

    [Required, MaxLength(64)]
    public string Name { get; init; }

    public Guid? ParentNotebookId { get; init; }

    public int? Order { get; init; }

    [EnumDataType(typeof(NotebookIconType))]
    public NotebookIconType IconType { get; init; }

    [MaxLength(262_144)]
    public string? IconSvg { get; init; }

    public Guid? IconAttachmentId { get; init; }
}
