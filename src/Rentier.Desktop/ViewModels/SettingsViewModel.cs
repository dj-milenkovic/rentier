using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

public sealed class SettingsViewModel : ReactiveObject
{
    public ProfileSettingsViewModel ProfileTab { get; }
    public HolidaySettingsViewModel HolidayTab { get; }
    public MailboxSettingsViewModel MailboxesTab { get; }
    public ImporterSettingsViewModel ImportersTab { get; }
    public AppearanceSettingsViewModel AppearanceTab { get; }

    public SettingsViewModel(
        ProfileSettingsViewModel profileTab,
        HolidaySettingsViewModel holidayTab,
        MailboxSettingsViewModel mailboxesTab,
        ImporterSettingsViewModel importersTab,
        AppearanceSettingsViewModel appearanceTab)
    {
        ProfileTab = profileTab;
        HolidayTab = holidayTab;
        MailboxesTab = mailboxesTab;
        ImportersTab = importersTab;
        AppearanceTab = appearanceTab;
    }
}