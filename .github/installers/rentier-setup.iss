; Rentier InnoSetup Script
; Build: iscc /DAppVersion=1.0.0 rentier-setup.iss
; CI overrides SourcePath, OutputPath, and IconPath via /D flags.
;
; /DVelopack builds the auto-updating variant: SourcePath is then Velopack's portable layout
; (Rentier.exe launcher + Update.exe + current\), installed to the same folder and under the
; same AppId as the classic installer, so either one upgrades a per-user install of the other
; in place. The auto-updating variant is per-user only, so it cannot upgrade a classic install
; made "for all users" (Program Files, registered under HKLM); it stops and asks the user to
; uninstall that copy first (see [Code]). Without /DVelopack the script produces the classic
; installer unchanged.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourcePath
  #define SourcePath "..\..\publish\win-x64"
#endif
#ifndef OutputPath
  #define OutputPath "..\..\artifacts"
#endif
#ifndef IconPath
  #define IconPath "..\..\Assets\Icons\rentier.ico"
#endif

#ifdef Velopack
  ; Velopack's launcher stub: survives updates, always starts the current version.
  #define MainExe "Rentier.exe"
  #define OutputSuffix "-velopack"
#else
  #define MainExe "Rentier.Desktop.exe"
  #define OutputSuffix ""
#endif

[Setup]
AppId={{F7A3B2C1-D4E5-4F6A-8B9C-0D1E2F3A4B5C}
AppName=Rentier
AppVersion={#AppVersion}
AppVerName=Rentier {#AppVersion}
AppPublisher=Rentier
AppPublisherURL=https://github.com/dj-milenkovic/rentier
DefaultDirName={autopf}\Rentier
DefaultGroupName=Rentier
DisableProgramGroupPage=yes
OutputDir={#OutputPath}
OutputBaseFilename=Rentier-{#AppVersion}-win-x64{#OutputSuffix}-setup
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\{#MainExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
#ifndef Velopack
; The auto-updating variant is always per-user: the app replaces its own files when it updates,
; which it cannot do inside an all-users Program Files install.
PrivilegesRequiredOverridesAllowed=dialog
#endif
LicenseFile=..\..\LICENSE
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

#ifdef Velopack
[InstallDelete]
; Files a previous classic install or earlier in-app updates left behind. Install folder only —
; user data lives in %LocalAppData%\Rentier and is never touched.
Type: files; Name: "{app}\Rentier.Desktop.exe"
Type: filesandordirs; Name: "{app}\current"
Type: filesandordirs; Name: "{app}\packages"
#endif

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rentier"; Filename: "{app}\{#MainExe}"; IconFilename: "{app}\{#MainExe}"
Name: "{group}\{cm:UninstallProgram,Rentier}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rentier"; Filename: "{app}\{#MainExe}"; IconFilename: "{app}\{#MainExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MainExe}"; Description: "{cm:LaunchProgram,Rentier}"; Flags: nowait postinstall skipifsilent

#ifdef Velopack
[UninstallDelete]
; In-app updates replace current\ and download into packages\, so uninstall cannot rely on
; its own file list for them. Install folder only — never the user data folder.
Type: filesandordirs; Name: "{app}\current"
Type: filesandordirs; Name: "{app}\packages"
#endif

#ifdef Velopack
[Code]
// A classic install made "for all users" lives in Program Files and is registered under HKLM.
// This per-user installer only sees HKCU, so it would add a second copy instead of upgrading:
// two entries in Apps, and the all-users shortcut keeps opening the old copy, which never updates.
const
  ClassicUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{F7A3B2C1-D4E5-4F6A-8B9C-0D1E2F3A4B5C}_is1';

function FindAllUsersInstall(var Location: String): Boolean;
begin
  Location := '';
  // IsWin64 first: HKLM64 raises an error on 32-bit Windows (Setup refuses to run there anyway).
  Result := IsWin64 and RegKeyExists(HKLM64, ClassicUninstallKey);
  if Result then
    RegQueryStringValue(HKLM64, ClassicUninstallKey, 'InstallLocation', Location)
  else
  begin
    Result := RegKeyExists(HKLM32, ClassicUninstallKey);
    if Result then
      RegQueryStringValue(HKLM32, ClassicUninstallKey, 'InstallLocation', Location);
  end;
end;

function InitializeSetup(): Boolean;
var
  Location: String;
begin
  Result := True;
  if FindAllUsersInstall(Location) then
  begin
    if Location = '' then
      Location := '(Program Files)';
    SuppressibleMsgBox(
      'Rentier is already installed for all users of this computer, in:' + #13#10 +
      Location + #13#10#13#10 +
      'The auto-updating version installs for your account only and cannot replace that copy.' + #13#10 +
      'Uninstall Rentier in Settings > Apps first, then run this installer again.' + #13#10#13#10 +
      'Your data is not affected: it is stored in your user profile, not in the install folder.',
      mbCriticalError, MB_OK, IDOK);
    Result := False;
  end;
end;
#endif
