; =====================================================================
;  MakriFormas Installer Script — Inno Setup 6
;  Generado para distribución sin base de datos preexistente.
;  La DB se crea automáticamente en el primer arranque de la app.
; =====================================================================

#define AppName      "MakriFormas"
#define AppVersion   "1.0.0"
#define AppPublisher "Makri"
#define AppExeName   "MakriFormas.exe"
#define AppURL       "https://makri.com"
; Ruta a la carpeta de publish generada por dotnet publish
#define SourceDir    "..\publish\MakriFormas"

[Setup]
; --- Identifiers ---
AppId={{F3A2B8C1-4D5E-4F6A-8B9C-0D1E2F3A4B5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

; --- Instalación ---
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; El ícono de la app en el instalador
SetupIconFile=..\MakriFormas\makriLogo.ico

; --- Salida ---
OutputDir=..\installer\output
OutputBaseFilename=MakriFormas_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra64

; --- Privilegios ---
; Admin requerido para instalar en Program Files
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

; --- UI ---
WizardStyle=modern
WizardSizePercent=120
ShowLanguageDialog=no

; --- Windows mínimo ---
MinVersion=10.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el &Escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Files]
; ---- Todos los archivos del publish, incluyendo ejecutable, imágenes y DLLs extras ----
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ---- Símbolo de depuración (opcional — puedes quitarlo) ----
; Source: "{#SourceDir}\MakriFormas.pdb";    DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Menú Inicio
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"

; Escritorio (solo si el usuario marcó la tarea)
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Opción para ejecutar la app al terminar la instalación
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Eliminar archivos generados en tiempo de ejecución dentro de la carpeta de instalación
; (La DB vive en %LocalAppData%\MakriFormas — NO se borra al desinstalar para conservar los datos)
Type: filesandordirs; Name: "{app}"

[Code]
// ----------------------------------------------------------------
// Página de bienvenida personalizada: informa que la DB se crea
// automáticamente y que los datos se guardan en AppData.
// ----------------------------------------------------------------
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'Este asistente instalará {#AppName} {#AppVersion} en su equipo.' + #13#10 + #13#10 +
    'La base de datos se creará automáticamente en su primera ejecución ' +
    'en la carpeta:' + #13#10 +
    '  %LocalAppData%\MakriFormas\' + #13#10 + #13#10 +
    'Sus datos NO se eliminarán al desinstalar la aplicación.';
end;
