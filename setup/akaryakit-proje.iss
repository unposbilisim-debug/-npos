#define MyAppName "Akaryakit Proje"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Unpos Bilisim"
#define MyAppExeName "UnposVardiyaTakip.exe"

[Setup]
AppId={{8F3C2A91-6B14-4D5E-9C20-A7E4B1D09F33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userdesktop}\Akaryakit Proje
DefaultGroupName=Akaryakit Proje
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AkaryakitProje-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=
ArchitecturesInstallIn64BitMode=x64compatible
AllowNoIcons=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaustu kisayolu olustur"; GroupDescription: "Kisayollar:"; Flags: checkedonce

[Files]
Source: "..\publish\UnposVardiyaTakip.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\samples\*"; DestDir: "{app}\samples"; Flags: ignoreversion
Source: "..\OKU.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\kaynak\*"; DestDir: "{app}\Kaynak"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Akaryakit Proje"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\Akaryakit Proje"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Kaynak Kod (Visual Studio)"; Filename: "{app}\Kaynak\UnposVardiyaTakip.sln"
Name: "{group}\Kaldir"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Programi simdi calistir"; Flags: nowait postinstall skipifsilent
