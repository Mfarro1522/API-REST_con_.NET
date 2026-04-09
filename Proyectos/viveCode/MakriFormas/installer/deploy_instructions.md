# Instrucciones para Desplegar y Empaquetar MakriFormas

El proyecto está configurado para compilarse y empaquetarse de forma **100% autónoma (Self-Contained)**. Esto significa que **el cliente final NO necesita instalar absolutamente ninguna dependencia** (ni .NET Desktop Runtime, ni SQLite, ni otras librerías). Todo queda incluido en el `.exe` compilado.

A continuación, los pasos para que en futuras versiones simplemente generes y distribuyas la aplicación tranquilamente:

### Requisitos en tu Máquina (Desarrollo)
1. Tener [Inno Setup 6](https://jrsoftware.org/isdl.php) instalado (si quieres generar un instalador tipo "Siguiente > Siguiente > Instalar").
2. Tener el SDK de .NET 10.
3. tener el png base de la proforma se llama 'proformaBase.png'

### Método 1: Crear un Instalador (.exe) Automático
Los archivos de la versión anterior (`build_installer.ps1` y `MakriFormas.iss`) se han adaptado para funcionar con la versión actual (he añadido un parámetro extra para asegurar que las librerías nativas como SQLite queden dentro del archivo único).
 
Para generar un nuevo instalador:
1. Abre una terminal de PowerShell como administrador (o directamente desde VS Code).
2. Entra en esta carpeta (`installer`): `cd installer`
3. Ejecuta el script de construcción: `.\build_installer.ps1`
4. Al terminar, tu instalador estara en la carpeta `installer/output` con el nombre `MakriFormas_Setup_1.0.0.exe` (el número de versión lo puedes editar en la parte superior del archivo `MakriFormas.iss`).

### Método 2: Solo generar el `.exe` Portable (Sin Instalador)
Si en alguna futura versión el cliente solo necesita el ejecutable portable para llevar en un USB o compartir suelto, puedes generarlo así desde la raíz donde está el `.sln` (o dentro de la carpeta del proyecto):

```powershell
dotnet publish MakriFormas/MakriFormas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

El archivo resultante (`MakriFormas.exe`) aparecerá dentro de `MakriFormas/bin/Release/net10.0-windows10.0.17763.0/win-x64/publish/` y es 100% independiente de cualquier instalación.

### Importante: Layout PDF editable en raíz
La aplicación usa un archivo de configuración llamado `pdf_layout.conf` para posiciones y tamaños del PDF.

1. Este archivo debe estar en la carpeta raíz del despliegue, junto a `MakriFormas.exe`.
2. Si no existe, cópialo desde `MakriFormas/pdf_layout.conf`.
3. Si existe también una versión en `config/pdf_layout.conf`, la aplicación prioriza el de la raíz.
4. Incluye siempre este archivo en cualquier instalador o paquete portable que envíes al cliente.

### Conclusión sobre Dependencias
Nuevamente, este `.exe` (o el instalador generado con Inno Setup) contiene todas las DLLs de WPF y QuestPDF/iText y la Runtime de .NET. Funciona "Out Of The Box" en prácticamente cualquier Windows x64.
