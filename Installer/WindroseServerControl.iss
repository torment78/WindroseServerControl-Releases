#define MyAppName "Windrose Server Control"
#define MyAppExeName "Elka_windrose_server_control.exe"
#define MyAppPublisher "ElkaSoft"
#define MyAppVersion GetEnv("WINDROSE_APP_VERSION")
#define PublishDir GetEnv("WINDROSE_PUBLISH_DIR")
#define OutputDir GetEnv("WINDROSE_OUTPUT_DIR")

[Setup]
AppId={{7D9D2F4C-5E9A-4D1A-B7A6-WINDROSE001}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autodesktop}\Windrose Server Control
DefaultGroupName=ElkaSoft\Windrose Server Control
OutputDir={#OutputDir}
OutputBaseFilename=WindroseServerControl_Setup_v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\Icons\windrose_server.ico
WizardStyle=modern
WizardSizePercent=100



[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Data\*,Tools\*,ServerFiles\*,Backups\*,WorldBackups\*,Profiles\*"

[Icons]
Name: "{group}\Windrose Server Control"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Windrose Server Control"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Windrose Server Control"; Flags: nowait postinstall skipifsilent