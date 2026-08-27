#define MyAppName "QP11汽配管理系统"
#define MyAppVersion "2.2.2"
#define MyAppPublisher "QP11"
#define MyAppExeName "QP11.Wpf.exe"

[Setup]
AppId={{B8E3F1A0-5C2D-4A6E-9F8B-1D3E5A7C9B02}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
OutputDir=f:\qp11\installer\output
OutputBaseFilename=QP11_Setup_{#MyAppVersion}
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinese"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
; 应用程序文件：覆盖安装，排除配置文件和Data目录（Data由运行时生成，包含用户业务数据）
Source: "f:\qp11\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.json,appsettings.Development.json,Data\*"
; 配置文件：仅当目标不存在时才写入，避免覆盖用户已有的配置；卸载时不删除
Source: "f:\qp11\publish\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "f:\qp11\publish\appsettings.Development.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载{#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动{#MyAppName}"; Flags: nowait postinstall skipifsilent

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
