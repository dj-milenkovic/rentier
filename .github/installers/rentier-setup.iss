; Rentier InnoSetup Script
; Build: iscc /DAppVersion=1.0.0 rentier-setup.iss
; CI overrides SourcePath, OutputPath, and IconPath via /D flags.

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

[Setup]
AppId={{F7A3B2C1-D4E5-4F6A-8B9C-0D1E2F3A4B5C}
AppName=Rentier
AppVersion={#AppVersion}
AppVerName=Rentier {#AppVersion}
AppPublisher=Rentier
AppPublisherURL=https://gitlab.com/djordje.milenkovic96/rentier
DefaultDirName={autopf}\Rentier
DefaultGroupName=Rentier
DisableProgramGroupPage=yes
OutputDir={#OutputPath}
OutputBaseFilename=Rentier-{#AppVersion}-win-x64-setup
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\Rentier.Desktop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
LicenseFile=..\..\LICENSE
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rentier"; Filename: "{app}\Rentier.Desktop.exe"; IconFilename: "{app}\Rentier.Desktop.exe"
Name: "{group}\{cm:UninstallProgram,Rentier}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rentier"; Filename: "{app}\Rentier.Desktop.exe"; IconFilename: "{app}\Rentier.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Rentier.Desktop.exe"; Description: "{cm:LaunchProgram,Rentier}"; Flags: nowait postinstall skipifsilent
