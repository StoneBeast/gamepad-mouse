; GamepadMouse 安装包脚本（Inno Setup 6）
; 版本号由 CI 传入：ISCC /DAppVersion=x.y.z
; 免管理员：按用户安装到 %LOCALAPPDATA%\Programs\GamepadMouse

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

[Setup]
AppId={{7C1E2A4F-9B36-4E8D-8A57-0F2D6C3B9E41}
AppName=GamepadMouse
AppVersion={#AppVersion}
AppPublisher=stoneBeast
AppPublisherURL=https://github.com/StoneBeast/gamepad-mouse
DefaultDirName={userpf}\GamepadMouse
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GamepadMouse-setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\GamepadMouse.exe
LicenseFile=..\LICENSE

[Languages]
#if FileExists(AddBackslash(CompilerPath) + "Languages\ChineseSimplified.isl")
; Inno Setup 6.4+ 官方内置简体中文
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
#else
; 旧版回退：仅英文向导
Name: "english"; MessagesFile: "compiler:Default.isl"
#endif

[Files]
Source: "..\dist\publish\GamepadMouse.exe"; DestDir: "{app}"; Flags: ignoreversion

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{userprograms}\GamepadMouse"; Filename: "{app}\GamepadMouse.exe"
Name: "{userdesktop}\GamepadMouse"; Filename: "{app}\GamepadMouse.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GamepadMouse.exe"; Description: "{cm:LaunchProgram,GamepadMouse}"; Flags: nowait postinstall skipifsilent

[Code]
// 卸载前先结束运行中的实例，避免文件占用导致卸载失败
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM GamepadMouse.exe /F', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
