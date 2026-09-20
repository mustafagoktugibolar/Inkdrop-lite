namespace Inkdrop_lite.Domain.Common;

/// <summary>Entities that record which client created and last modified them.</summary>
public interface ISourceTracked
{
    string CreatedSource { get; set; }

    string UpdatedSource { get; set; }
}
