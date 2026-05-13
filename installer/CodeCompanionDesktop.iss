#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\CodeCompanionDesktop-win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{9C1E1D68-7D19-4A2E-A8E1-4B64617C55F5}
AppName=Code Companion Desktop
AppVersion={#AppVersion}
AppPublisher=Code Companion
DefaultDirName={localappdata}\Programs\Code Companion Desktop
DefaultGroupName=Code Companion Desktop
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=CodeCompanionDesktopSetup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\CodeCompanionDesktop\Assets\app.ico
UninstallDisplayIcon={app}\CodeCompanionDesktop.exe
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Code Companion Desktop"; Filename: "{app}\CodeCompanionDesktop.exe"
Name: "{autodesktop}\Code Companion Desktop"; Filename: "{app}\CodeCompanionDesktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodeCompanionDesktop.exe"; Description: "Launch Code Companion Desktop"; Flags: nowait postinstall skipifsilent
