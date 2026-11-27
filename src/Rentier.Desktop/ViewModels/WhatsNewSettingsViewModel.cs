using System.Collections.ObjectModel;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Settings > What's new: the release notes built into the running copy (its version and the
/// ones before it), in the UI language. Reloads when the language changes.
/// </summary>
public sealed class WhatsNewSettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IQueryHandler<GetInstalledReleaseNotesQuery, Result<IReadOnlyList<ReleaseNoteDto>, Error>> _getNotes;
    private readonly ILocalizationService _localization;
    private readonly IAppVersionService _appVersion;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public WhatsNewSettingsViewModel(
        IQueryHandler<GetInstalledReleaseNotesQuery, Result<IReadOnlyList<ReleaseNoteDto>, Error>> getNotes,
        ILocalizationService localization,
        IAppVersionService appVersion)
    {
        _getNotes = getNotes;
        _localization = localization;
        _appVersion = appVersion;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);

        this.WhenActivated((MultipleDisposable disposables) =>
        {
            LoadCommand.Execute().Subscribe().DisposeWith(disposables);

            _localization.CultureChanged
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(SubheaderText));
                    LoadCommand.Execute().Subscribe();
                })
                .DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    /// <summary>One entry per version, newest first.</summary>
    public ObservableCollection<ReleaseNoteDto> Notes { get; } = [];

    public ReactiveCommand<RxVoid, RxVoid> LoadCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public bool HasItems => Notes.Count > 0;

    /// <summary>Loaded without error, but this build carries no notes (e.g. a local dev build).</summary>
    public bool IsEmpty => !IsLoading && !HasItems && string.IsNullOrEmpty(ErrorMessage);

    public string SubheaderText =>
        string.Format(_localization[nameof(Strings.WhatsNew_Subheader)], _appVersion.DisplayVersion);

    /// <summary>GitHub Releases, for versions older than the notes cover.</summary>
    public Uri? ReleasesPageUri => ReleasesPage.Uri;

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _getNotes.HandleAsync(new GetInstalledReleaseNotesQuery(_localization.CurrentCultureCode));

            Notes.Clear();
            if (result.IsSuccess)
            {
                foreach (var note in result.Value)
                    Notes.Add(note);
            }
            else
            {
                ErrorMessage = _localization[nameof(Strings.WhatsNew_LoadError)];
            }

            this.RaisePropertyChanged(nameof(HasItems));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
