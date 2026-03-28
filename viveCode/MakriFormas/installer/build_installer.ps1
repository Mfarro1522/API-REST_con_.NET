$ErrorActionPreference = "Stop"

$PublishDir = Join-Path $PSScriptRoot "..\publish\MakriFormas"
$InstallerOutputDir = Join-Path $PSScriptRoot "output"
$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\Iscc.exe"
$ProjectFile = Join-Path $PSScriptRoot "..\MakriFormas\MakriFormas.csproj"
$IssFile = Join-Path $PSScriptRoot "MakriFormas.iss"

Write-Host "=== Generando versión de distribución limpia ==="

Write-Host "[1/4] Limpiando carpetas antiguas..."
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
if (Test-Path $InstallerOutputDir) { Remove-Item -Recurse -Force $InstallerOutputDir }

Write-Host "[2/4] Compilando y publicando MakriFormas (Win-x64, Self-Contained, Single File)..."
# Usamos dotnet publish asegurándonos de que salga como un solo archivo con las dependencias net necesarias
# y sin generar ni copiar la base de datos a publish.
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Falló la compilación."
    exit $LASTEXITCODE
}

Write-Host "[3/4] Limpiando artefactos no deseados (db o datos de ejemplo)..."
# Asegurarnos de que no haya ninguna db copiada accidentalmente en publish
if (Test-Path "$PublishDir\makriformas.db") {
    Write-Host "  -> Eliminando makriformas.db"
    Remove-Item -Force "$PublishDir\makriformas.db"
}

Write-Host "[4/4] Creando instalador con Inno Setup..."
if (Test-Path $InnoSetupCompiler) {
    & $InnoSetupCompiler $IssFile
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "=== ¡Instalador generado correctamente en '$InstallerOutputDir'! ===" -ForegroundColor Green
    } else {
        Write-Error "Ocurrió un error ejecutando Inno Setup."
    }
} else {
    Write-Warning "No se encontró Inno Setup 6 en la ruta '$InnoSetupCompiler'."
    Write-Warning "Por favor, instala Inno Setup 6 (https://jrsoftware.org/isdl.php) o compila el archivo MakriFormas.iss manualmente."
}
