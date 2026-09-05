; WoodStreamFileSync - Inno Setup Script
; プロジェクトルール:
; - スタンドアロンインストーラは exe 形式で .\Installer フォルダに作成し、ファイル名にはバージョン番号を含めること。
; - 実行環境のアーキテクチャ (x64, arm64) に合わせたものを作成すること。

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

#ifndef MyAppArch
  #define MyAppArch "x64"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "Installer"
#endif

#ifndef MySourceDir
  #define MySourceDir "publish\win-" + MyAppArch
#endif

#define MyAppName "WoodStreamFileSync"
#define MyAppPublisher "tkizawa"
#define MyAppExeName "WoodStreamFileSync.exe"
#define MyOutputBaseFilename "WoodStreamFileSync_v" + MyAppVersion + "_" + MyAppArch + "_Setup"

[Setup]
AppId={{D68F2F7D-42AC-4BE3-B077-B79BC58A8A1E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 管理者権限不要でもインストール可能（ユーザー権限または全ユーザー両対応）
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile=Resources\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; アーキテクチャに応じた制限設定
#if MyAppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 自己完結型 publish 成果物を配置
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
