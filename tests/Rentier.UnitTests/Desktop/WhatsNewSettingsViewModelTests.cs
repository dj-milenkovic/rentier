using FluentAssertions;
using NSubstitute;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class WhatsNewSettingsViewModelTests
{
    private static readonly ReleaseNoteDto[] Notes =
    [
        new("v1.4.0", "- New"),
        new("v1.3.1", "- Old"),
    ];

    private sealed record Harness(
        WhatsNewSettingsViewModel Vm,
        IQueryHandler<GetInstalledReleaseNotesQuery, Result<IReadOnlyList<ReleaseNoteDto>, Error>> GetNotes,
        ILocalizationService Localization,
        Signal<string> CultureChanged);

    private static Harness Build(Result<IReadOnlyList<ReleaseNoteDto>, Error>? result = null, string culture = "en")
    {
        var getNotes = Substitute.For<IQueryHandler<GetInstalledReleaseNotesQuery, Result<IReadOnlyList<ReleaseNoteDto>, Error>>>();
        getNotes.HandleAsync(Arg.Any<GetInstalledReleaseNotesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result ?? Result<IReadOnlyList<ReleaseNoteDto>, Error>.Success(Notes));

        var cultureChanged = new Signal<string>();
        var localization = Substitute.For<ILocalizationService>();
        localization.CurrentCultureCode.Returns(culture);
        localization.CultureChanged.Returns(cultureChanged);
        localization["WhatsNew_Subheader"].Returns("Installed: {0}");
        localization["WhatsNew_LoadError"].Returns("Could not load.");

        var appVersion = Substitute.For<IAppVersionService>();
        appVersion.DisplayVersion.Returns("v1.4.0");

        return new Harness(new WhatsNewSettingsViewModel(getNotes, localization, appVersion), getNotes, localization, cultureChanged);
    }

    [Fact]
    public async Task LoadCommand_Success_FillsNotesNewestFirst()
    {
        var h = Build();

        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        h.Vm.Notes.Should().Equal(Notes);
        h.Vm.HasItems.Should().BeTrue();
        h.Vm.IsEmpty.Should().BeFalse();
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadCommand_AsksForTheUiLanguage()
    {
        var h = Build(culture: "sr-Latn");

        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await h.GetNotes.Received(1).HandleAsync(
            Arg.Is<GetInstalledReleaseNotesQuery>(q => q.CultureCode == "sr-Latn"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadCommand_NoNotes_IsEmpty()
    {
        var h = Build(Result<IReadOnlyList<ReleaseNoteDto>, Error>.Success([]));

        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        h.Vm.HasItems.Should().BeFalse();
        h.Vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadCommand_Failure_ShowsLocalizedErrorAndIsNotEmpty()
    {
        var h = Build(Result<IReadOnlyList<ReleaseNoteDto>, Error>.Failure(new Error(ErrorCodes.INFRASTRUCTURE_ERROR, "boom")));

        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        h.Vm.ErrorMessage.Should().Be("Could not load.");
        h.Vm.HasItems.Should().BeFalse();
        h.Vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadCommand_RunTwice_DoesNotDuplicateNotes()
    {
        var h = Build();

        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        await h.Vm.LoadCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        h.Vm.Notes.Should().HaveCount(2);
    }

    [Fact]
    public void SubheaderText_ShowsInstalledVersion()
    {
        Build().Vm.SubheaderText.Should().Be("Installed: v1.4.0");
    }

    [Fact]
    public void ReleasesPageUri_ComesFromBuildMetadata()
    {
        Build().Vm.ReleasesPageUri.Should().NotBeNull();
        Build().Vm.ReleasesPageUri!.Scheme.Should().Be(Uri.UriSchemeHttps);
    }

    [Fact]
    public async Task Activation_LoadsNotes_AndReloadsWhenLanguageChanges()
    {
        var h = Build();

        using (h.Vm.Activator.Activate())
        {
            for (var i = 0; i < 100 && h.Vm.Notes.Count == 0; i++)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            h.CultureChanged.OnNext("sr-Latn");
            for (var i = 0; i < 100 && h.GetNotes.ReceivedCalls().Count() < 2; i++)
                await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        h.Vm.Notes.Should().HaveCount(2);
        await h.GetNotes.Received(2).HandleAsync(Arg.Any<GetInstalledReleaseNotesQuery>(), Arg.Any<CancellationToken>());
    }
}
