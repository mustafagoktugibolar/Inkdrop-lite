using Inkdrop_lite.Features.Mcp;

namespace Inkdrop_lite.Tests.Features.Mcp;

public sealed class NoteContentReaderTests
{
    private const string Sample =
        "# Title\nintro\n## Setup\nsteps\n```\n# not a heading\n```\n## Usage\nrun it\n### Flags\n-v\n# Appendix\nend\n";

    [Fact]
    public void Outline_finds_headings_and_ignores_code_fences()
    {
        var outline = NoteContentReader.BuildOutline(Sample);

        Assert.Equal(
            ["Title", "Setup", "Usage", "Flags", "Appendix"],
            outline.Select(entry => entry.Heading));
        Assert.Equal([1, 2, 2, 3, 1], outline.Select(entry => entry.Level));
    }

    [Fact]
    public void Section_spans_until_the_next_heading_of_the_same_or_higher_level()
    {
        var page = NoteContentReader.Read(Sample, "usage", 0, 5_000);

        Assert.Equal("Usage", page.Section);
        Assert.Equal("## Usage\nrun it\n### Flags\n-v\n", page.Content);
        Assert.False(page.HasMore);
    }

    [Fact]
    public void Short_notes_are_returned_whole_without_an_outline()
    {
        var page = NoteContentReader.Read(Sample, null, 0, 5_000);

        Assert.Equal(Sample, page.Content);
        Assert.False(page.HasMore);
        Assert.Null(page.Outline);
        Assert.Null(page.Hint);
    }

    [Fact]
    public void Long_notes_are_paged_with_an_outline_and_reassemble_losslessly()
    {
        var content = "# Big\n" + string.Concat(Enumerable.Range(0, 900).Select(i => $"line {i}\n"));
        var assembled = new System.Text.StringBuilder();
        var offset = 0;
        ContentPage? first = null;

        while (true)
        {
            var page = NoteContentReader.Read(content, null, offset, 1_000);
            first ??= page;
            assembled.Append(page.Content);

            if (!page.HasMore)
            {
                break;
            }

            offset = page.NextOffset!.Value;
        }

        Assert.Equal(content, assembled.ToString());
        Assert.True(first!.HasMore);
        Assert.NotNull(first.Outline);
        Assert.Contains("offset=", first.Hint);
    }

    [Fact]
    public void Unknown_section_lists_available_headings()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            NoteContentReader.Read(Sample, "nope", 0, 5_000));

        Assert.Contains("Setup", error.Message);
    }

    [Fact]
    public void Paging_never_splits_a_surrogate_pair()
    {
        var content = new string('a', 999) + "😀" + new string('b', 50);

        var page = NoteContentReader.Read(content, null, 0, 1_000);

        Assert.False(char.IsHighSurrogate(page.Content[^1]));
    }
}
