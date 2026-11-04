#define AppName "RSS Label Maker"
#define AppPublisher "Razeen Secure Solution"
#define AppExeName "RSS.LabelMaker.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "1.0.0.0"
#endif
#ifndef AppExeSource
  #define AppExeSource "..\artifacts\publish\RSS.LabelMaker.exe"
#endif
[Setup]
AppId={{B89B1E55-2E66-4B11-A6D2-1D98E7C8B1A4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Razeen Secure Solution\RSS Label Maker
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename=RSS-Label-Maker-Setup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoVersion={#AppFileVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Windows Installer
VersionInfoProductName={#AppName}
SetupLogging=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
[Files]
Source: "{#AppExeSource}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion
[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent
[Code]
function InitializeSetup(): Boolean;
var
  ScriptPath, ScriptText, Params: String;
  ResultCode: Integer;
begin
  Result := True;
  ScriptPath := ExpandConstant('{tmp}\rlm-installer-update-check.ps1');
  ScriptText :=
    '$ErrorActionPreference = ''Stop''' + #13#10 +
    'Add-Type -AssemblyName System.Windows.Forms' + #13#10 +
    '$current = [version]''' + '{#AppVersion}' + '''' + #13#10 +
    '$api = ''https://api.github.com/repos/riyazamra1/RSS-Label-Maker-/releases/latest''' + #13#10 +
    'try {' + #13#10 +
    '  $release = Invoke-RestMethod -Uri $api -Headers @{''User-Agent''=''RSS-Label-Maker-Setup''} -TimeoutSec 12' + #13#10 +
    '  if ($release.draft -or $release.prerelease) { throw ''No stable release available.'' }' + #13#10 +
    '  $latest = [version](([string]$release.tag_name).TrimStart(''v'',''V''))' + #13#10 +
    '  if ($latest -le $current) { exit 0 }' + #13#10 +
    '  $installer = $release.assets | Where-Object { $_.name -eq ''RSS-Label-Maker-Setup.exe'' } | Select-Object -First 1' + #13#10 +
    '  $checksum = $release.assets | Where-Object { $_.name -eq ''RSS-Label-Maker-Setup.exe.sha256'' } | Select-Object -First 1' + #13#10 +
    '  if (!$installer -or !$checksum) { throw ''Latest release is missing installer/checksum assets.'' }' + #13#10 +
    '  $temp = Join-Path $env:TEMP (''RSS-Label-Maker-Setup-'' + [guid]::NewGuid().ToString())' + #13#10 +
    '  New-Item -ItemType Directory -Path $temp | Out-Null' + #13#10 +
    '  $setupPath = Join-Path $temp ''RSS-Label-Maker-Setup.exe''' + #13#10 +
    '  $hashPath = Join-Path $temp ''RSS-Label-Maker-Setup.exe.sha256''' + #13#10 +
    '  Invoke-WebRequest -Uri $installer.browser_download_url -OutFile $setupPath -TimeoutSec 180' + #13#10 +
    '  Invoke-WebRequest -Uri $checksum.browser_download_url -OutFile $hashPath -TimeoutSec 30' + #13#10 +
    '  $expected = ((Get-Content -Raw $hashPath).Trim() -split ''\s+'')[0].ToLowerInvariant()' + #13#10 +
    '  $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $setupPath).Hash.ToLowerInvariant()' + #13#10 +
    '  if ($expected -notmatch ''^[a-f0-9]{64}$'' -or $actual -ne $expected) { throw ''Downloaded installer failed SHA-256 verification.'' }' + #13#10 +
    '  $choice = [System.Windows.Forms.MessageBox]::Show((''RSS Label Maker '' + $latest + '' is available. Supplied version: '' + $current + ''. A newer installer was downloaded and verified. Install latest now?''), ''RSS Label Maker Update Available'', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Information)' + #13#10 +
    '  if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) { Start-Process -FilePath $setupPath; exit 10 }' + #13#10 +
    '  exit 0' + #13#10 +
    '} catch {' + #13#10 +
    '  $choice = [System.Windows.Forms.MessageBox]::Show((''Could not check/download the latest installer. '' + $_.Exception.Message + [Environment]::NewLine + [Environment]::NewLine + ''Continue with this installer in offline mode?''), ''RSS Label Maker Offline Installation'', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Warning)' + #13#10 +
    '  if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) { exit 20 }' + #13#10 +
    '  exit 30' + #13#10 +
'}' + #13#10;
  if not SaveStringToFile(ScriptPath, ScriptText, False) then
  begin
    MsgBox('Could not prepare the update check. Setup will continue with the supplied package.', mbInformation, MB_OK);
    Exit;
  end;
  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ScriptPath + '"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if MsgBox('Online update check could not start. Continue in offline mode?', mbConfirmation, MB_YESNO) = IDNO then Result := False;
    Exit;
  end;
  if (ResultCode = 10) or (ResultCode = 30) then Result := False
  else if (ResultCode <> 0) and (ResultCode <> 20) then
    if MsgBox('Update check failed unexpectedly. Continue with supplied installer?', mbConfirmation, MB_YESNO) = IDNO then Result := False;
end;
