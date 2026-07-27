using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI.Reactive;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Working-copy flyout ViewModel for text-based column filters (paying entity, payment ref, deadline).
/// <para>
/// Pattern: on open → copy committed text into <see cref="WorkingText"/>; Apply → commit;
/// dismiss without Apply → working text is discarded on next open.
/// </para>
/// </summary>
public sealed class TextFilterFlyoutViewModel : ReactiveObject
{
    private readonly Subject<Unit> _applied = new();
    private bool _isOpen;
    private bool _isActive;
    private string? _committed;
    private string? _workingText;

    /// <summary>Fires each time the user clicks Apply.</summary>
    public IObservable<Unit> Applied => _applied.AsObservable();

    /// <summary>Whether the flyout popup is currently open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            // Sync working text from committed state when the flyout is opened.
            if (value && !_isOpen)
                WorkingText = _committed;
            this.RaiseAndSetIfChanged(ref _isOpen, value);
        }
    }

    /// <summary>True when a non-empty text filter is committed.</summary>
    public bool IsActive
    {
        get => _isActive;
        private set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>The text currently visible in the flyout text box (working copy).</summary>
    public string? WorkingText
    {
        get => _workingText;
        set => this.RaiseAndSetIfChanged(ref _workingText, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleOpenCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    public TextFilterFlyoutViewModel()
    {
        ToggleOpenCommand = ReactiveCommand.Create(() => { IsOpen = !IsOpen; });

        ApplyCommand = ReactiveCommand.Create(() =>
        {
            _committed = string.IsNullOrEmpty(_workingText) ? null : _workingText;
            IsActive = _committed is not null;
            IsOpen = false;
            _applied.OnNext(Unit.Default);
        });
    }

    /// <summary>Returns the committed filter text, or <c>null</c> if no filter is active.</summary>
    public string? GetCommittedValue() => _committed;

    /// <summary>
    /// Clears the committed filter and working text.
    /// Does NOT fire <see cref="Applied"/>; the caller is responsible for triggering a reload.
    /// </summary>
    public void Clear()
    {
        _committed = null;
        WorkingText = null;
        IsActive = false;
    }
}
