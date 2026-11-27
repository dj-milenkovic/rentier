namespace Rentier.Desktop.Controls.Markdown;

// The small Markdown model MarkdownParser produces and MarkdownView renders.

/// <summary>A block-level Markdown element.</summary>
public abstract record MdBlock;

/// <summary><c># Title</c> to <c>###### Title</c>.</summary>
public sealed record MdHeading(int Level, IReadOnlyList<MdInline> Content) : MdBlock;

/// <summary>Consecutive text lines.</summary>
public sealed record MdParagraph(IReadOnlyList<MdInline> Content) : MdBlock;

/// <summary>A bullet (<c>-</c>, <c>*</c>, <c>+</c>) or numbered (<c>1.</c>) list.</summary>
public sealed record MdList(bool IsOrdered, int Start, IReadOnlyList<MdListItem> Items) : MdBlock;

/// <summary>One list item: its text and any nested lists.</summary>
public sealed record MdListItem(IReadOnlyList<MdBlock> Blocks);

/// <summary><c>&gt; quoted</c> lines.</summary>
public sealed record MdQuote(IReadOnlyList<MdBlock> Blocks) : MdBlock;

/// <summary>A fenced (<c>```</c>) code block.</summary>
public sealed record MdCodeBlock(string Code) : MdBlock;

/// <summary><c>---</c>, <c>***</c> or <c>___</c> on its own line.</summary>
public sealed record MdRule : MdBlock;

/// <summary>An inline Markdown element.</summary>
public abstract record MdInline;

/// <summary>Plain text.</summary>
public sealed record MdText(string Text) : MdInline;

/// <summary><c>**bold**</c> or <c>__bold__</c>.</summary>
public sealed record MdBold(IReadOnlyList<MdInline> Content) : MdInline;

/// <summary><c>*italic*</c> or <c>_italic_</c>.</summary>
public sealed record MdItalic(IReadOnlyList<MdInline> Content) : MdInline;

/// <summary><c>~~struck~~</c>.</summary>
public sealed record MdStrikethrough(IReadOnlyList<MdInline> Content) : MdInline;

/// <summary><c>`code`</c>.</summary>
public sealed record MdCode(string Code) : MdInline;

/// <summary><c>[text](url)</c>. <see cref="Url"/> is as written; the view decides whether it is safe to open.</summary>
public sealed record MdLink(string Url, IReadOnlyList<MdInline> Content) : MdInline;

/// <summary>A hard line break (a line ending in two spaces or a backslash).</summary>
public sealed record MdLineBreak : MdInline;
