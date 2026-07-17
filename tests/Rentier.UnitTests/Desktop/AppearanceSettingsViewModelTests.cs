using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class AppearanceSettingsViewModelTests
{
    private static (AppearanceSettingsViewModel vm, ILocalizationService loc, ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>> handler)
        Build(string initialCulture = "sr-Latn")
    {
        var themeService = Substitute.For<IThemeService>();
        themeService.GetPreference().Returns(ThemePreference.System);

        var locService = Substitute.For<ILocalizationService>();
        locService.CurrentCultureCode.Returns(initialCulture);
        // Make CultureChanged observable never emit (test-only stub)
        locService.CultureChanged.Returns(System.Reactive.Linq.Observable.Never<string>());

        var setHandler = Substitute.For<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>();
        setHandler.HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var vm = new AppearanceSettingsViewModel(themeService, locService, setHandler);
        return (vm, locService, setHandler);
    }

    // ── T029: Initial SelectedLanguage matches localizationService.CurrentCultureCode ──

    [Fact]
    public void Constructor_SelectedLanguage_MatchesCurrentCultureCode()
    {
        var (vm, loc, _) = Build("sr-Latn");

        vm.SelectedLanguage.Should().Be("sr-Latn");
    }

    [Fact]
    public void Constructor_SelectedLanguage_EnglishWhenCultureIsEn()
    {
        var (vm, _, _) = Build("en");

        vm.SelectedLanguage.Should().Be("en");
    }

    // ── T029: Selecting language calls SetUserPreferenceCommand ──────────────

    [Fact]
    public async Task SelectLanguage_English_CallsSetUserPreferenceCommand()
    {
        var (vm, loc, handler) = Build("sr-Latn");

        vm.SelectedLanguage = "en";

        await Task.Delay(50, TestContext.Current.CancellationToken); // allow async fire-and-forget
        await handler.Received(1).HandleAsync(
            Arg.Is<SetUserPreferenceCommand>(c => c!.Key == "Language" && c.Value == "en"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectLanguage_SameLanguageTwice_DoesNotTriggerDuplicateCommand()
    {
        var (vm, _, handler) = Build("sr-Latn");

        // Change to English
        vm.SelectedLanguage = "en";
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Change to English again (same value) - should be deduplicated by DistinctUntilChanged
        vm.SelectedLanguage = "en";
        await Task.Delay(50, TestContext.Current.CancellationToken);

        await handler.Received(1).HandleAsync(
            Arg.Is<SetUserPreferenceCommand>(c => c!.Value == "en"),
            Arg.Any<CancellationToken>());
    }

    // ── T033: LanguageOptions contains exactly 2 entries ─────────────────────

    [Fact]
    public void LanguageOptions_ContainsExactlyTwoEntries()
    {
        AppearanceSettingsViewModel.LanguageOptions.Should().HaveCount(2);
    }

    [Fact]
    public void LanguageOptions_ContainsEnglishEntry()
    {
        AppearanceSettingsViewModel.LanguageOptions
            .Should().Contain(opt => opt.Code == "en" && opt.DisplayName == "English");
    }

    [Fact]
    public void LanguageOptions_ContainsSrLatnEntry()
    {
        AppearanceSettingsViewModel.LanguageOptions
            .Should().Contain(opt => opt.Code == "sr-Latn" && opt.DisplayName == "Srpski");
    }

    [Fact]
    public void LanguageOptions_LabelsAreInOwnLanguage_NotLocalizerBound()
    {
        // English label must always be "English" regardless of culture
        var englishOption = AppearanceSettingsViewModel.LanguageOptions
            .First(o => o.Code == "en");
        englishOption.DisplayName.Should().Be("English");

        // Srpski label must always be "Srpski" regardless of culture
        var serbianOption = AppearanceSettingsViewModel.LanguageOptions
            .First(o => o.Code == "sr-Latn");
        serbianOption.DisplayName.Should().Be("Srpski");
    }
}
