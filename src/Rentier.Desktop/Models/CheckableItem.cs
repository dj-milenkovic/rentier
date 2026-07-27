using ReactiveUI.Reactive;

namespace Rentier.Desktop.Models;

/// <summary>
/// A labelled, observable checkbox item used in enum filter flyouts.
/// Holds a typed <typeparamref name="T"/> value and a mutable <see cref="IsChecked"/> flag.
/// </summary>
public sealed class CheckableItem<T> : ReactiveObject where T : notnull
{
    private bool _isChecked;

    public string Label { get; }
    public T Value { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    public CheckableItem(string label, T value, bool isChecked = true)
    {
        Label = label;
        Value = value;
        _isChecked = isChecked;
    }
}
