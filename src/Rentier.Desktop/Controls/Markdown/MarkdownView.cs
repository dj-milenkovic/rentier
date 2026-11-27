using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rentier.Desktop.Controls.Markdown;

/// <summary>
/// Shows Markdown (as read by <see cref="MarkdownParser"/>) as native Avalonia text. Only
/// <c>https</c> links are clickable (they open in the browser); any other link shows as plain
/// text. Text colour is inherited (<c>TextElement.Foreground</c>) and every other colour is a
/// theme resource, so the view follows the light and dark themes.
/// </summary>
public sealed class MarkdownView : Decorator
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private const string BORDER_BRUSH = "RentierBorderBrush";
    private const string CODE_BACKGROUND_BRUSH = "RentierCardElevatedBrush";
    private const string LINK_BRUSH = "RentierAccentBrush";

    private static readonly FontFamily Monospace = new("Consolas, Menlo, monospace");

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
            Child = Render(MarkdownParser.Parse(Markdown));
    }

    /// <summary>Builds the control tree for parsed Markdown.</summary>
    internal static StackPanel Render(IReadOnlyList<MdBlock> blocks)
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var block in blocks)
            panel.Children.Add(RenderBlock(block, listDepth: 0));
        return panel;
    }

    private static Control RenderBlock(MdBlock block, int listDepth) => block switch
    {
        MdHeading heading => Heading(heading),
        MdParagraph paragraph => Text(paragraph.Content),
        MdList list => List(list, listDepth),
        MdQuote quote => Quote(quote, listDepth),
        MdCodeBlock code => CodeBlock(code.Code),
        MdRule => new Separator { Margin = new Thickness(0, 4) },
        _ => new Panel(),
    };

    private static SelectableTextBlock Heading(MdHeading heading)
    {
        var title = Text(heading.Content);
        title.FontWeight = FontWeight.SemiBold;
        title.FontSize = heading.Level <= 2 ? 16 : 14;
        title.Margin = new Thickness(0, 4, 0, 0);
        return title;
    }

    private static StackPanel List(MdList list, int depth)
    {
        var panel = new StackPanel { Spacing = 3 };
        var number = list.Start;

        foreach (var item in list.Items)
        {
            var marker = list.IsOrdered ? $"{number++}." : (depth == 0 ? "•" : "◦");
            var content = new StackPanel { Spacing = 3 };
            foreach (var child in item.Blocks)
                content.Children.Add(RenderBlock(child, depth + 1));

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(list.IsOrdered ? "24,*" : "16,*") };
            row.Children.Add(new TextBlock { Text = marker, HorizontalAlignment = HorizontalAlignment.Left });
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Border Quote(MdQuote quote, int depth)
    {
        var content = new StackPanel { Spacing = 4 };
        foreach (var child in quote.Blocks)
            content.Children.Add(RenderBlock(child, depth));

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

    private static Border CodeBlock(string code)
    {
        var border = new Border
        {
            Padding = new Thickness(8, 6),
            CornerRadius = new CornerRadius(4),
            Child = new SelectableTextBlock
            {
                Text = code,
                FontFamily = Monospace,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable(CODE_BACKGROUND_BRUSH));
        return border;
    }

    private static SelectableTextBlock Text(IReadOnlyList<MdInline> content)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        text.Inlines!.AddRange(Inlines(content));
        return text;
    }

    private static IEnumerable<Inline> Inlines(IReadOnlyList<MdInline> content)
    {
        foreach (var inline in content)
        {
            switch (inline)
            {
                case MdText text:
                    yield return new Run(text.Text);
                    break;
                case MdCode code:
                    yield return new Run(code.Code) { FontFamily = Monospace };
                    break;
                case MdLineBreak:
                    yield return new LineBreak();
                    break;
                case MdBold bold:
                    yield return Wrap(new Bold(), bold.Content);
                    break;
                case MdItalic italic:
                    yield return Wrap(new Italic(), italic.Content);
                    break;
                case MdStrikethrough struck:
                    yield return Wrap(new Span { TextDecorations = TextDecorations.Strikethrough }, struck.Content);
                    break;
                case MdLink link:
                    yield return Link(link);
                    break;
            }
        }
    }

    private static Span Wrap(Span span, IReadOnlyList<MdInline> content)
    {
        span.Inlines.AddRange(Inlines(content));
        return span;
    }

    private static Inline Link(MdLink link)
    {
        var label = Wrap(new Span(), link.Content);
        if (!IsSafeLink(link.Url, out var uri))
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
    internal static bool IsSafeLink(string url, out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;
}
