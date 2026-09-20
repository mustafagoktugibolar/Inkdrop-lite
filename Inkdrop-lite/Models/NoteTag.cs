using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class NoteTag : UserOwnedEntity
{
    public Guid NoteId { get; set; }

    public Note Note { get; set; } = null!;

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;
}
