; QP11汽配管理系统 安装程序脚本
; Inno Setup 6.7.3
; 生成日期: 2026-06-07
; 自包含发布：已内置 .NET 8.0 运行时，无需预装环境

#define MyAppName "QP11汽配管理系统"
#define MyAppVersion "2.2.12"
#define MyAppPublisher "QP11"
#define MyAppURL "http://www.qp11.com/"
#define MyAppExeName "QP11.Wpf.exe"
#define MyAppSourceDir "publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; LicenseFile=LICENSE.txt
; InfoBeforeFile=README.txt
OutputDir=InstallPackage
OutputBaseFilename=QP11Setup_v{#MyAppVersion}
; SetupIconFile=app.ico  ; 可选：添加程序图标
SetupIconFile=InstallPackage\app_full.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
VersionInfoVersion={#MyAppVersion}
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}
MinVersion=6.1sp1
AlwaysShowDirOnReadyPage=yes
DisableWelcomePage=no
DisableFinishedPage=no
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; 主文件：排除用户配置文件和Data目录，避免覆盖（Data由运行时生成，包含用户业务数据）
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.json,printsettings.json,Data\*"
; 保留用户配置文件：重新安装时若已存在则不覆盖（数据库连接）
Source: "{#MyAppSourceDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
; 保留用户配置文件：重新安装时若已存在则不覆盖（打印设置，运行时生成）
Source: "{#MyAppSourceDir}\printsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKLM; Subkey: "SOFTWARE\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\{#MyAppName}"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName}"; Flags: uninsdeletevalue

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 保护Data目录：卸载时询问是否删除用户业务数据
    if DirExists(ExpandConstant('{app}\Data')) then
    begin
      if MsgBox('是否删除业务数据文件夹（Data）？含配件库存、交易覆盖等数据，建议保留。', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(ExpandConstant('{app}\Data'), True, True, True);
      end;
    end;
    if DirExists(ExpandConstant('{app}\logs')) then
    begin
      if MsgBox('是否删除日志文件夹？', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(ExpandConstant('{app}\logs'), True, True, True);
      end;
    end;
  end;
end;
