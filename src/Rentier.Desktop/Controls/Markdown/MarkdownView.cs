using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace Rentier.Desktop.Controls.Markdown;

/// <summary>
/// Shows Markdown (parsed by Markdig: CommonMark plus <c>~~strikethrough~~</c>) as native Avalonia
/// text. Only <c>https</c> links are clickable (they open in the browser); any other link shows
/// as plain text. Images are never loaded (their alt text is shown) and raw HTML shows as text.
/// Text colour is inherited (<c>TextElement.Foreground</c>) and every other colour is a theme
/// resource, so the view follows the light and dark themes.
/// </summary>
public sealed class MarkdownView : Decorator
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private const string BORDER_BRUSH = "RentierBorderBrush";
    private const string CODE_BACKGROUND_BRUSH = "RentierCardElevatedBrush";
    private const string LINK_BRUSH = "RentierAccentBrush";

    /// <summary>Deeper blocks and inlines are shown as plain text, so hostile nesting cannot exhaust the stack.</summary>
    private const int MAX_NESTING = 8;

    private static readonly FontFamily Monospace = new("Consolas, Menlo, monospace");

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .DisableHtml()
        .Build();

    /// <summary>The Markdown to show.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Child = Render(Markdown);
    }

    /// <summary>Builds the control tree for <paramref name="markdown"/>.</summary>
    internal static StackPanel Render(string? markdown)
    {
        var panel = new StackPanel { Spacing = 6 };
        if (string.IsNullOrWhiteSpace(markdown))
            return panel;

        if (Parse(markdown) is { } document)
            AddBlocks(panel, document, depth: 0, listDepth: 0);
        else
            panel.Children.Add(new SelectableTextBlock { Text = markdown, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    /// <summary>
    /// Parses <paramref name="markdown"/>, or returns null when Markdig rejects it: Markdig throws
    /// <see cref="ArgumentException"/> for input nested deeper than its limit (128), and notes that
    /// absurd are shown as plain text rather than crashing the dialog.
    /// </summary>
    private static MarkdownDocument? Parse(string markdown)
    {
        try
        {
            return Markdig.Markdown.Parse(markdown, Pipeline);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void AddBlocks(Panel panel, ContainerBlock container, int depth, int listDepth)
    {
        foreach (var block in container)
        {
            if (RenderBlock(block, depth, listDepth) is { } control)
                panel.Children.Add(control);
        }
    }

    private static Control? RenderBlock(Block block, int depth, int listDepth)
    {
        if (depth >= MAX_NESTING)
            return Flattened(block);

        return block switch
        {
            HeadingBlock heading => Heading(heading),
            ParagraphBlock paragraph => Text(paragraph.Inline),
            ListBlock list => List(list, depth, listDepth),
            QuoteBlock quote => Quote(quote, depth, listDepth),
            CodeBlock code => Code(code),
            ThematicBreakBlock => new Separator { Margin = new Thickness(0, 4) },
            _ => null,
        };
    }

    /// <summary>The text of a too-deeply nested block, one plain line per leaf.</summary>
    private static SelectableTextBlock Flattened(Block block)
    {
        IEnumerable<LeafBlock> leaves = block is LeafBlock leaf ? [leaf] : block.Descendants<LeafBlock>();
        var lines = leaves.Select(l => l.Inline is { } inline ? PlainText(inline) : l.Lines.ToString());
        return new SelectableTextBlock
        {
            Text = string.Join("\n", lines.Where(line => line.Length > 0)),
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static SelectableTextBlock Heading(HeadingBlock heading)
    {
        var title = Text(heading.Inline);
        title.FontWeight = FontWeight.SemiBold;
        title.FontSize = heading.Level <= 2 ? 16 : 14;
        title.Margin = new Thickness(0, 4, 0, 0);
        return title;
    }

    private static StackPanel List(ListBlock list, int depth, int listDepth)
    {
        var panel = new StackPanel { Spacing = 3 };
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{number++}." : (listDepth == 0 ? "•" : "◦");
            var content = new StackPanel { Spacing = 3 };
            AddBlocks(content, item, depth + 1, listDepth + 1);

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(list.IsOrdered ? "24,*" : "16,*") };
            row.Children.Add(new TextBlock { Text = marker, HorizontalAlignment = HorizontalAlignment.Left });
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Border Quote(QuoteBlock quote, int depth, int listDepth)
    {
        var content = new StackPanel { Spacing = 4 };
        AddBlocks(content, quote, depth + 1, listDepth);

        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            Opacity = 0.85,
            Child = content,
        };
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable(BORDER_BRUSH));
        return border;
    }

    private static Border Code(CodeBlock code)
    {
        var border = new Border
        {
            Padding = new Thickness(8, 6),
            CornerRadius = new CornerRadius(4),
            Child = new SelectableTextBlock
            {
                Text = code.Lines.ToString().TrimEnd('\n', '\r'),
                FontFamily = Monospace,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable(CODE_BACKGROUND_BRUSH));
        return border;
    }

    private static SelectableTextBlock Text(ContainerInline? content)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        if (content is not null)
            text.Inlines!.AddRange(Inlines(content, depth: 0));
        return text;
    }

    private static IEnumerable<AvaloniaInline> Inlines(ContainerInline container, int depth)
    {
        foreach (var inline in container)
        {
            if (RenderInline(inline, depth) is { } rendered)
                yield return rendered;
        }
    }

    private static AvaloniaInline? RenderInline(MdInline inline, int depth)
    {
        if (depth >= MAX_NESTING)
            return new Run(PlainText(inline));

        return inline switch
        {
            LiteralInline literal => new Run(literal.Content.ToString()),
            HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
            CodeInline code => new Run(code.Content) { FontFamily = Monospace },
            LineBreakInline { IsHard: true } => new LineBreak(),
            LineBreakInline => new Run(" "),
            EmphasisInline emphasis => Wrap(Emphasis(emphasis), emphasis, depth),
            LinkInline { IsImage: true } image => Wrap(new Span(), image, depth),
            LinkInline link => Link(link.Url, Wrap(new Span(), link, depth)),
            AutolinkInline { IsEmail: false } autolink => Link(autolink.Url, new Span { Inlines = { new Run(autolink.Url) } }),
            AutolinkInline autolink => new Run(autolink.Url),
            ContainerInline other => Wrap(new Span(), other, depth),
            _ => null,
        };
    }

    private static Span Emphasis(EmphasisInline emphasis) => emphasis switch
    {
        { DelimiterChar: '~' } => new Span { TextDecorations = TextDecorations.Strikethrough },
        { DelimiterCount: >= 2 } => new Bold(),
        _ => new Italic(),
    };

    private static Span Wrap(Span span, ContainerInline content, int depth)
    {
        span.Inlines.AddRange(Inlines(content, depth + 1));
        return span;
    }

    private static AvaloniaInline Link(string? url, Span label)
    {
        if (!IsSafeLink(url, out var uri))
            return label;

        var labelText = new TextBlock { TextDecorations = TextDecorations.Underline };
        labelText.Inlines!.Add(label);
        labelText.Bind(TextBlock.ForegroundProperty, labelText.GetResourceObservable(LINK_BRUSH));
        return new InlineUIContainer(new HyperlinkButton
        {
            NavigateUri = uri,
            Padding = new Thickness(0),
            Content = labelText,
        });
    }

    /// <summary>Only absolute <c>https</c> links may open; release notes are text from PR descriptions.</summary>
    internal static bool IsSafeLink(string? url, out Uri? uri)
    {
        uri = null;
        return url is not null
            && Uri.TryCreate(url, UriKind.Absolute, out uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>The text of an inline and everything inside it, without markup.</summary>
    private static string PlainText(MdInline inline) => inline switch
    {
        LiteralInline literal => literal.Content.ToString(),
        HtmlEntityInline entity => entity.Transcoded.ToString(),
        CodeInline code => code.Content,
        LineBreakInline => " ",
        AutolinkInline autolink => autolink.Url,
        ContainerInline container => string.Concat(container.Descendants<MdInline>()
            .Where(d => d is not ContainerInline)
            .Select(PlainText)),
        _ => string.Empty,
    };
}
