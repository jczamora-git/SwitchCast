; =====================================================================
; SwitchCast — Professional Windows Installation Wizard (Inno Setup 6)
; Target Version: 1.5.0
; Architecture: Windows x64 (Unpackaged WinUI 3 / .NET 8 Self-Contained)
; Publisher: John Christopher King Zamora
; =====================================================================

#define MyAppName "SwitchCast"
#define MyAppVersion "1.5.0"
#define MyAppPublisher "John Christopher King Zamora"
#define MyAppURL "https://switchcast.wasmer.app/"
#define MyAppExeName "SwitchCast.exe"
#define MyAppId "{{E5D91C08-36D4-4B7E-9721-7B32F7FEA780}"
#define MyPublishDir "..\publish"

[Setup]
; Stable application identity (preserved across all updates)
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Destination directory & per-user default installation
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Output file configuration
OutputDir=..\releases
OutputBaseFilename=SwitchCast-Setup-v{#MyAppVersion}-x64
SetupIconFile=..\Assets\SwitchCast.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Modern Windows Setup Wizard styling
WizardStyle=modern
WizardSizePercent=100

; Compression strategy: Ultra LZMA2 for minimal download footprint
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra64

; Architecture and Platform Constraints (Windows 10 version 1809+ / Windows 11 x64)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; User Privilege Strategy: Default to lowest (per-user, no UAC needed), allow machine-wide override
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Safety & File in-use handling
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

; Authenticode Code Signing & Uninstaller Protection (enabled via /DEnableSigning)
#ifdef EnableSigning
SignTool=standard $f
SignedUninstaller=yes
SignToolRetryCount=3
SignToolRetryDelay=2000
#endif

; Add/Remove Programs display properties
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Self-contained publish payload (excluding PDB debug symbols to reduce payload size)
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
; Start Menu shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\SwitchCast.ico"; Comment: "High-performance Windows desktop application for screen sharing and presentation management."
; Optional Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\SwitchCast.ico"; Tasks: desktopicon; Comment: "SwitchCast — Screen Sharing & Presentation Manager"

[Run]
; Option to launch SwitchCast immediately after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Detect running SwitchCast instances using WMI to prevent locked files during install/uninstall
function IsAppRunning(const FileName: string): Boolean;
var
  FSWbemLocator: Variant;
  FWMIService: Variant;
  FWbemObjectSet: Variant;
begin
  Result := False;
  try
    FSWbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2');
    FWbemObjectSet := FWMIService.ExecQuery(
      Format('SELECT ProcessId FROM Win32_Process WHERE Name = "%s"', [FileName]));
    Result := (FWbemObjectSet.Count > 0);
  except
    Result := False;
  end;
end;

function CheckRunningAppPrompt(const ActionDesc: string): Boolean;
var
  PromptMsg: string;
begin
  Result := True;
  PromptMsg := 'SwitchCast is currently running.' + #13#10 + #13#10 +
               'Please close SwitchCast (including any active presentation, SwitchDeck dock, or music playback) before ' +
               ActionDesc + '.' + #13#10 + #13#10 +
               'Click Retry once closed, or Cancel to exit setup.';
  while IsAppRunning('SwitchCast.exe') do
  begin
    if MsgBox(PromptMsg, mbConfirmation, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := CheckRunningAppPrompt('continuing with the installation');
end;

function InitializeUninstall(): Boolean;
begin
  Result := CheckRunningAppPrompt('continuing with the uninstallation');
end;
