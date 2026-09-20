using System.Text.RegularExpressions;

namespace Inkdrop_lite.Features.Mcp;

/// <summary>A Markdown heading and the span of text it owns (up to the next heading of the same or higher level).</summary>
public sealed record OutlineEntry(int Level, string Heading, int StartChar, int Length);

public sealed record ContentPage(
    string Content,
    int TotalChars,
    int Offset,
    bool HasMore,
    int? NextOffset,
    string? Section,
    IReadOnlyList<OutlineEntry>? Outline,
    string? Hint);

/// <summary>
/// Lets an agent read a large note in pieces instead of pulling up to 1 MiB into its context:
/// a heading outline, section-by-heading reads, and character paging.
/// </summary>
public static partial class NoteContentReader
{
    public const int DefaultPageChars = 12_000;
    private const int MinPageChars = 1_000;
    private const int MaxPageChars = 100_000;
    private const int MaxOutlineEntries = 200;

    [GeneratedRegex(@"^(?<hashes>#{1,6})[ \t]+(?<text>.*?)[ \t]*#*[ \t]*$")]
    private static partial Regex HeadingPattern();

    public static IReadOnlyList<OutlineEntry> BuildOutline(string content)
    {
        var headings = new List<(int Level, string Text, int Start)>();
        string? fence = null;
        var position = 0;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal) ||
                trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                var marker = trimmed[..3];
                fence = fence is null ? marker : fence == marker ? null : fence;
            }
            else if (fence is null && HeadingPattern().Match(line) is { Success: true } match &&
                     match.Groups["text"].Value.Length > 0)
            {
                headings.Add((match.Groups["hashes"].Length, match.Groups["text"].Value, position));
            }

            position += rawLine.Length + 1;
        }

        var outline = new List<OutlineEntry>(headings.Count);

        for (var index = 0; index < headings.Count; index++)
        {
            var (level, text, start) = headings[index];
            var end = content.Length;

            for (var next = index + 1; next < headings.Count; next++)
            {
                if (headings[next].Level <= level)
                {
                    end = headings[next].Start;
                    break;
                }
            }

            outline.Add(new OutlineEntry(level, text, start, end - start));
        }

        return outline;
    }

    /// <exception cref="ArgumentException">The requested section does not exist.</exception>
    public static ContentPage Read(string content, string? section, int offset, int maxChars)
    {
        maxChars = Math.Clamp(maxChars, MinPageChars, MaxPageChars);
        var outline = BuildOutline(content);
        string? resolvedSection = null;
        var text = content;

        if (!string.IsNullOrWhiteSpace(section))
        {
            var entry = outline.FirstOrDefault(candidate => string.Equals(
                    candidate.Heading, section.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? outline.FirstOrDefault(candidate => candidate.Heading.Contains(
                    section.Trim(), StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                throw new ArgumentException(outline.Count == 0
                    ? $"Section '{section}' was not found: the note has no headings."
                    : $"Section '{section}' was not found. Available headings: " +
                      string.Join("; ", outline.Take(50).Select(item => item.Heading)));
            }

            resolvedSection = entry.Heading;
            text = content.Substring(entry.StartChar, entry.Length);
        }

        var start = Math.Clamp(offset, 0, text.Length);
        var end = Math.Min(start + maxChars, text.Length);

        if (end < text.Length)
        {
            var lineBreak = text.LastIndexOf('\n', end - 1, end - start);

            if (lineBreak > start + (end - start) / 2)
            {
                end = lineBreak + 1;
            }

            if (end > start && char.IsHighSurrogate(text[end - 1]))
            {
                end--;
            }
        }

        var hasMore = end < text.Length;
        var showOutline = outline.Count > 0 && (hasMore || start > 0 || content.Length > maxChars);

        return new ContentPage(
            text[start..end],
            text.Length,
            start,
            hasMore,
            hasMore ? end : null,
            resolvedSection,
            showOutline ? outline.Take(MaxOutlineEntries).ToList() : null,
            hasMore
                ? $"Content truncated at {end} of {text.Length} characters. Call again with " +
                  $"offset={end} for the next page" +
                  (outline.Count > 0 ? ", or pass section=<heading> to read one section." : ".")
                : null);
    }
}
