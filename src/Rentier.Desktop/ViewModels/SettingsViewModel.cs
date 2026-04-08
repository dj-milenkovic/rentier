using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

public sealed class SettingsViewModel : ReactiveObject
{
    public ProfileSettingsViewModel ProfileTab { get; }
    public HolidaySettingsViewModel HolidayTab { get; }
    public MailboxSettingsViewModel MailboxesTab { get; }
    public ImporterSettingsViewModel ImportersTab { get; }

    public SettingsViewModel(
        ProfileSettingsViewModel profileTab,
        HolidaySettingsViewModel holidayTab,
        MailboxSettingsViewModel mailboxesTab,
        ImporterSettingsViewModel importersTab)
    {
        ProfileTab = profileTab;
        HolidayTab = holidayTab;
        MailboxesTab = mailboxesTab;
        ImportersTab = importersTab;
    }
}