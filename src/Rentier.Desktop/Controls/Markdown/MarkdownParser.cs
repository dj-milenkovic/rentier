using System.Text;
using System.Text.RegularExpressions;

namespace Rentier.Desktop.Controls.Markdown;

/// <summary>
/// A small Markdown parser for release notes. It covers what notes are written with:
/// headings, paragraphs, bullet and numbered lists (nested), quotes, fenced code, horizontal
/// rules, bold, italic, strikethrough, inline code, links and backslash escapes.
/// Anything else (tables, images, raw HTML, reference links) stays as plain text.
/// Never throws.
/// </summary>
public static partial class MarkdownParser
{
    private const int MAX_NESTING = 8;

    /// <summary>Parses <paramref name="markdown"/> into blocks.</summary>
    public static IReadOnlyList<MdBlock> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\t", "    ", StringComparison.Ordinal)
            .Split('\n');
        return ParseBlocks(lines, depth: 0);
    }

    private static List<MdBlock> ParseBlocks(IReadOnlyList<string> lines, int depth)
    {
        var blocks = new List<MdBlock>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
                blocks.Add(new MdParagraph(ParseParagraphInlines(paragraph)));
            paragraph.Clear();
        }

        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                i++;
                continue;
            }

            if (Fence().IsMatch(line))
            {
                FlushParagraph();
                var code = new List<string>();
                i++;
                while (i < lines.Count && !Fence().IsMatch(lines[i]))
                    code.Add(lines[i++]);
                i++; // closing fence (or end of input)
                blocks.Add(new MdCodeBlock(string.Join('\n', code)));
                continue;
            }

            var heading = Heading().Match(line);
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new MdHeading(heading.Groups["marks"].Length, ParseInlines(heading.Groups["text"].Value)));
                i++;
                continue;
            }

            if (Rule().IsMatch(line))
            {
                FlushParagraph();
                blocks.Add(new MdRule());
                i++;
                continue;
            }

            if (Quote().IsMatch(line) && depth < MAX_NESTING)
            {
                FlushParagraph();
                var quoted = new List<string>();
                while (i < lines.Count && Quote().Match(lines[i]) is { Success: true } q)
                {
                    quoted.Add(q.Groups["text"].Value);
                    i++;
                }

                blocks.Add(new MdQuote(ParseBlocks(quoted, depth + 1)));
                continue;
            }

            var item = ListItem().Match(line);
            if (item.Success && item.Groups["indent"].Length <= 3 && depth < MAX_NESTING)
            {
                FlushParagraph();
                i = ParseList(lines, i, depth, blocks);
                continue;
            }

            paragraph.Add(line);
            i++;
        }

        FlushParagraph();
        return blocks;
    }

    /// <summary>Parses the list starting at <paramref name="start"/> and returns the index after it.</summary>
    private static int ParseList(IReadOnlyList<string> lines, int start, int depth, List<MdBlock> blocks)
    {
        var first = ListItem().Match(lines[start]);
        var baseIndent = first.Groups["indent"].Length;
        var isOrdered = char.IsDigit(first.Groups["marker"].Value[0]);
        var startNumber = isOrdered && int.TryParse(first.Groups["marker"].Value.TrimEnd('.', ')'), out var n) ? n : 1;

        var items = new List<MdListItem>();
        List<string>? current = null;
        var contentIndent = 0;

        void FlushItem()
        {
            if (current is not null)
                items.Add(new MdListItem(ParseBlocks(current, depth + 1)));
        }

        var i = start;
        while (i < lines.Count)
        {
            var line = lines[i];
            var match = ListItem().Match(line);
            var indent = LeadingSpaces(line);

            if (match.Success && indent <= baseIndent + 1)
            {
                FlushItem();
                current = [match.Groups["text"].Value];
                contentIndent = indent + match.Groups["marker"].Length + 1;
                i++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                // A blank line ends the list unless the list goes on after it.
                var next = i + 1 < lines.Count ? lines[i + 1] : null;
                if (next is null || (LeadingSpaces(next) <= baseIndent + 1 && !ListItem().IsMatch(next)))
                    break;

                current!.Add(string.Empty);
                i++;
                continue;
            }

            // A continuation line: indented under the item, or a lazy continuation of its text.
            if (indent > baseIndent + 1 || !StartsBlock(line))
            {
                current!.Add(line[Math.Min(contentIndent, indent)..]);
                i++;
                continue;
            }

            break;
        }

        FlushItem();
        blocks.Add(new MdList(isOrdered, startNumber, items));
        return i;
    }

    private static bool StartsBlock(string line) =>
        Fence().IsMatch(line) || Heading().IsMatch(line) || Rule().IsMatch(line) || Quote().IsMatch(line);

    private static int LeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
            count++;
        return count;
    }

    private static List<MdInline> ParseParagraphInlines(IReadOnlyList<string> lines)
    {
        var inlines = new List<MdInline>();
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var hardBreak = raw.EndsWith("  ", StringComparison.Ordinal) || raw.TrimEnd().EndsWith('\\');
            var text = raw.Trim();
            if (text.EndsWith('\\'))
                text = text[..^1];

            inlines.AddRange(ParseInlines(text));
            if (i < lines.Count - 1)
                inlines.Add(hardBreak ? new MdLineBreak() : new MdText(" "));
        }

        return MergeText(inlines);
    }

    /// <summary>Parses inline Markdown in one line of text.</summary>
    public static IReadOnlyList<MdInline> ParseInlines(string text) => MergeText(ParseInlines(text, depth: 0));

    private static List<MdInline> ParseInlines(string text, int depth)
    {
        var result = new List<MdInline>();
        var plain = new StringBuilder();

        void FlushPlain()
        {
            if (plain.Length > 0)
                result.Add(new MdText(plain.ToString()));
            plain.Clear();
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\\' && i + 1 < text.Length && (char.IsPunctuation(text[i + 1]) || char.IsSymbol(text[i + 1])))
            {
                plain.Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (c == '`')
            {
                var close = text.IndexOf('`', i + 1);
                if (close > i + 1)
                {
                    FlushPlain();
                    result.Add(new MdCode(text[(i + 1)..close]));
                    i = close + 1;
                    continue;
                }
            }

            if (depth < MAX_NESTING)
            {
                if (TryDelimited(text, i, "**", out var inner, out var next) || TryDelimited(text, i, "__", out inner, out next))
                {
                    FlushPlain();
                    result.Add(new MdBold(ParseInlines(inner, depth + 1)));
                    i = next;
                    continue;
                }

                if (TryDelimited(text, i, "~~", out inner, out next))
                {
                    FlushPlain();
                    result.Add(new MdStrikethrough(ParseInlines(inner, depth + 1)));
                    i = next;
                    continue;
                }

                if ((c == '*' || (c == '_' && (i == 0 || !char.IsLetterOrDigit(text[i - 1]))))
                    && TryDelimited(text, i, c.ToString(), out inner, out next)
                    && (next >= text.Length || c == '*' || !char.IsLetterOrDigit(text[next])))
                {
                    FlushPlain();
                    result.Add(new MdItalic(ParseInlines(inner, depth + 1)));
                    i = next;
                    continue;
                }

                if (c == '[' && TryLink(text, i, out var label, out var url, out next))
                {
                    FlushPlain();
                    result.Add(new MdLink(url, ParseInlines(label, depth + 1)));
                    i = next;
                    continue;
                }
            }

            plain.Append(c);
            i++;
        }

        FlushPlain();
        return result;
    }

    /// <summary>
    /// Matches <paramref name="delimiter"/>…<paramref name="delimiter"/> at <paramref name="start"/>
    /// with non-blank content that neither starts nor ends with a space.
    /// </summary>
    private static bool TryDelimited(string text, int start, string delimiter, out string inner, out int next)
    {
        inner = string.Empty;
        next = start;
        if (!text.AsSpan(start).StartsWith(delimiter, StringComparison.Ordinal))
            return false;

        var contentStart = start + delimiter.Length;
        if (contentStart >= text.Length || char.IsWhiteSpace(text[contentStart]))
            return false;

        // A single-character delimiter must not be half of a doubled one (** is bold, not italic).
        var searchFrom = contentStart;
        while (true)
        {
            var close = text.IndexOf(delimiter, searchFrom, StringComparison.Ordinal);
            if (close < 0)
                return false;

            // "***" closing "**bold *italic***": the bold closes on the last two stars.
            while (delimiter.Length == 2 && close + 2 < text.Length && text[close + 2] == delimiter[0])
                close++;

            var doubled = delimiter.Length == 1 && close + 1 < text.Length && text[close + 1] == delimiter[0];
            if (close > contentStart && !char.IsWhiteSpace(text[close - 1]) && !doubled)
            {
                inner = text[contentStart..close];
                next = close + delimiter.Length;
                return true;
            }

            searchFrom = doubled ? close + 2 : close + 1;
        }
    }

    private static bool TryLink(string text, int start, out string label, out string url, out int next)
    {
        label = url = string.Empty;
        next = start;

        var closeBracket = text.IndexOf("](", start + 1, StringComparison.Ordinal);
        if (closeBracket < 0)
            return false;

        var closeParen = text.IndexOf(')', closeBracket + 2);
        if (closeParen < 0)
            return false;

        label = text[(start + 1)..closeBracket];
        url = text[(closeBracket + 2)..closeParen].Trim();
        if (label.Length == 0 || url.Length == 0 || url.Contains(' ', StringComparison.Ordinal))
            return false;

        next = closeParen + 1;
        return true;
    }

    private static List<MdInline> MergeText(List<MdInline> inlines)
    {
        var merged = new List<MdInline>(inlines.Count);
        foreach (var inline in inlines)
        {
            if (inline is MdText text && merged.Count > 0 && merged[^1] is MdText previous)
                merged[^1] = new MdText(previous.Text + text.Text);
            else
                merged.Add(inline);
        }

        return merged;
    }

    [GeneratedRegex(@"^ {0,3}```", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Fence();

    [GeneratedRegex(@"^ {0,3}(?<marks>#{1,6})\s+(?<text>.*?)(\s+#+)?\s*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^ {0,3}([-*_])( *\1){2,} *$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Rule();

    [GeneratedRegex(@"^ {0,3}> ?(?<text>.*)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quote();

    [GeneratedRegex(@"^(?<indent> *)(?<marker>[-*+]|\d{1,9}[.)])( +(?<text>.*))?$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListItem();
}
