using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI.Reactive;

namespace Rentier.Desktop;

/// <summary>
/// Resolves View type from ViewModel type by convention:
/// FooViewModel → FooView
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "No ViewModel provided" };

        var name = param.GetType().FullName!
            .Replace("ViewModels", "Views")
            .Replace("ViewModel", "View");

        var type = Type.GetType(name);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View not found: {name}" };
    }

    public bool Match(object? data) => data is ReactiveObject;
}
