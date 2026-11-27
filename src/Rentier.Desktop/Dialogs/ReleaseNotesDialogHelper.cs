using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Rentier.Desktop.Controls.Markdown;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.Dialogs;

/// <summary>Localized texts for <see cref="ReleaseNotesDialogHelper"/>.</summary>
internal sealed record ReleaseNotesDialogLabels(string Title, string UpdateNow, string Close, string OlderReleases);

/// <summary>
/// Modal window listing release notes, one card per version, newest first. Built in code like
/// <see cref="ConfirmDialogHelper"/>; every colour is a theme resource.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a real window.")]
internal static class ReleaseNotesDialogHelper
{
    public static async Task<bool> ShowAsync(
        ReleaseNotesDialogRequest request, ReleaseNotesDialogLabels labels, Uri? releasesPage)
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
            return false;

        var notes = new StackPanel { Spacing = 12 };
        foreach (var note in request.Notes)
        {
            var card = new Border
            {
                Classes = { "card" },
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Themed(new TextBlock { Text = note.Version, FontSize = 15, FontWeight = FontWeight.SemiBold },
                            TextBlock.ForegroundProperty, "RentierTextPrimaryBrush"),
                        Themed(new MarkdownView { Markdown = note.Markdown },
                            TextElement.ForegroundProperty, "RentierTextPrimaryBrush"),
                    },
                },
            };
            notes.Children.Add(Themed(card, Border.BorderBrushProperty, "RentierBorderBrush"));
        }

        if (releasesPage is not null)
            notes.Children.Add(new HyperlinkButton { Content = labels.OlderReleases, NavigateUri = releasesPage, Padding = new Thickness(0) });

        var closeButton = new Button { Content = labels.Close, IsCancel = true };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        Button? updateButton = null;
        if (request.OfferUpdate)
        {
            updateButton = new Button { Content = labels.UpdateNow, Classes = { "accent" }, IsDefault = true };
            buttons.Children.Add(updateButton);
        }

        buttons.Children.Add(closeButton);

        var header = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Themed(new TextBlock { Text = labels.Title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                    TextBlock.ForegroundProperty, "RentierTextPrimaryBrush"),
                Themed(new TextBlock { Text = request.Subheader, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                    TextBlock.ForegroundProperty, "RentierTextSecondaryBrush"),
            },
        };

        var layout = new DockPanel { Margin = new Thickness(20), LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        header.Margin = new Thickness(0, 0, 0, 12);
        buttons.Margin = new Thickness(0, 12, 0, 0);
        layout.Children.Add(header);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = notes,
        });

        var dialog = new Window
        {
            Title = labels.Title,
            Width = 560,
            Height = 520,
            MinWidth = 420,
            MinHeight = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = layout,
        };
        Themed(dialog, Window.BackgroundProperty, "RentierRegionBrush");

        closeButton.Click += (_, _) => dialog.Close(false);
        if (updateButton is not null)
            updateButton.Click += (_, _) => dialog.Close(true);

        return await dialog.ShowDialog<bool>(owner);
    }

    private static T Themed<T>(T control, AvaloniaProperty property, string resourceKey) where T : Control
    {
        control.Bind(property, control.GetResourceObservable(resourceKey));
        return control;
    }
}
