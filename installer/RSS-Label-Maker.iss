#define MyAppName "RSS Label Maker"
#define MyAppVersion GetEnv("RLM_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "1.0.0"
#endif
#define MyPublisher "Razeen Secure Solution"
#define MyExeName "RSS.LabelMaker.exe"

[Setup]
AppId={{B5F2B0B6-1F0A-4D52-9A3A-4A1F5D0B2A81}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={autopf}\Razeen Secure Solution\RSS Label Maker
DefaultGroupName=RSS Label Maker
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=RSS-Label-Maker-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyExeName}
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
VersionInfoCompany={#MyPublisher}
VersionInfoDescription=RSS Label Maker Windows Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RSS Label Maker"; Filename: "{app}\{#MyExeName}"
Name: "{autodesktop}\RSS Label Maker"; Filename: "{app}\{#MyExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyExeName}"; Description: "Launch RSS Label Maker"; Flags: postinstall nowait skipifsilent

[Code]
var
  AboutPage: TWizardPage;
  ProjectsPage: TWizardPage;
  AboutText: TNewStaticText;
  ProjectsText: TNewStaticText;

procedure InitializeWizard;
begin
  WizardForm.Caption := 'RSS Label Maker Setup';
  WizardForm.WelcomeLabel1.Caption := 'Welcome to RSS Label Maker';
  WizardForm.WelcomeLabel2.Caption :=
    'A professional product-label workspace from Razeen Secure Solution.' + #13#10#13#10 +
    'Create product records, prepare label layouts and work with your Windows printing environment. ' +
    'Your local label workspace is designed to remain available offline.';

  AboutPage := CreateCustomPage(wpWelcome, 'About RSS Label Maker',
    'Product information and the main capabilities of the application.');
  AboutText := TNewStaticText.Create(AboutPage);
  AboutText.Parent := AboutPage.Surface;
  AboutText.Left := 0;
  AboutText.Top := 0;
  AboutText.Width := AboutPage.SurfaceWidth;
  AboutText.Height := AboutPage.SurfaceHeight;
  AboutText.AutoSize := False;
  AboutText.WordWrap := True;
  AboutText.Font.Size := 10;
  AboutText.Caption :=
    'RSS LABEL MAKER' + #13#10 +
    'Product label design and production for Windows' + #13#10#13#10 +
    'PRODUCT WORKSPACE' + #13#10 +
    '• Organise product records by category.' + #13#10 +
    '• Maintain English, Sinhala and Tamil product fields.' + #13#10 +
    '• Store product information locally on this PC.' + #13#10#13#10 +
    'LABEL PRODUCTION' + #13#10 +
    '• Configure label dimensions and paper sizes.' + #13#10 +
    '• Prepare label layouts and review the preview.' + #13#10 +
    '• Discover installed Windows printers.' + #13#10#13#10 +
    'ACCOUNT AND CONNECTED SERVICES' + #13#10 +
    '• Offline label work does not require an RSS account.' + #13#10 +
    '• Sign in when you want account-linked licensing or cloud services.' + #13#10#13#10 +
    'Some advanced design, printing, import/export and connected features may be delivered in later application updates. ' +
    'Availability is determined by the installed application version.';

  ProjectsPage := CreateCustomPage(AboutPage.ID, 'The RSS Project Family',
    'A quick introduction to products developed by Razeen Secure Solution.');
  ProjectsText := TNewStaticText.Create(ProjectsPage);
  ProjectsText.Parent := ProjectsPage.Surface;
  ProjectsText.Left := 0;
  ProjectsText.Top := 0;
  ProjectsText.Width := ProjectsPage.SurfaceWidth;
  ProjectsText.Height := ProjectsPage.SurfaceHeight;
  ProjectsText.AutoSize := False;
  ProjectsText.WordWrap := True;
  ProjectsText.Font.Size := 10;
  ProjectsText.Caption :=
    'RAZEEN SECURE SOLUTION' + #13#10 +
    'A software, security and technology solutions company.' + #13#10#13#10 +
    'RSS CORE' + #13#10 +
    'Shared account, licensing and central application services.' + #13#10#13#10 +
    'RSS AI ASSISTANT' + #13#10 +
    'AI-assisted tools and productivity workflows.' + #13#10#13#10 +
    'RSS ORBINEX' + #13#10 +
    'Business operations and inventory management.' + #13#10#13#10 +
    'RSS MONEY MANAGER' + #13#10 +
    'Personal and business financial management.' + #13#10#13#10 +
    'RSS INVOICE MAKER' + #13#10 +
    'Invoice preparation and document workflows.' + #13#10#13#10 +
    'RSS CLOUD SYNC' + #13#10 +
    'Cloud synchronisation solutions.' + #13#10#13#10 +
    'These products are introduced as part of the RSS product family. ' +
    'They are not required to install or use RSS Label Maker, and availability may vary by product.';
end;
