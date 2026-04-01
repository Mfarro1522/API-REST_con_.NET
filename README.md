# API REST con .NET

Este repositorio contiene una colección de proyectos de aprendizaje y práctica desarrollados con .NET, enfocados en la creación de APIs REST y una aplicación de escritorio WPF para gestión de inventario.

## 📁 Estructura del Proyecto

```
API-REST_con_.NET/
├── Basico/                    # Proyectos básicos de API REST
│   ├── Api_basico_1/         # API básica con controladores y CORS
│   ├── ApiOllama/            # Integración con Ollama para IA local
│   ├── MinimalApi_Dados/     # Minimal API para simular lanzamiento de dados
│   └── PrimerApi/            # Primera API con Minimal API
├── viveCode/                  # Proyecto de aplicación de escritorio
│   └── MakriFormas/          # Sistema de gestión de inventario y proformas
├── .github/                  # Configuración de GitHub Actions
└── README.md                 # Este archivo
```

## 🚀 Proyectos Incluidos

### 1. **ApiOllama**
API que integra Ollama para interactuar con modelos de IA local:
- Endpoint `GET /api/chat/modelos` para listar modelos disponibles
- Endpoint `POST /api/chat` para enviar mensajes a la IA
- Uso de `HttpClient` para comunicarse con Ollama (puerto 11434)
- Ejemplo de consumo de APIs externas desde .NET

**Tecnologías:** .NET 10.0, ASP.NET Core, Ollama, HttpClient

**Requisitos:**
- Tener Ollama instalado y ejecutando (`ollama serve`)
- Modelos de IA descargados (ej: llama3, qwen2.5)

**Ejecutar:**
```bash
cd Basico/ApiOllama/ApiOllama
dotnet run
```

### 2. **MakriFormas** (Aplicación de escritorio)
Sistema de gestión de inventario y proformas desarrollado en WPF:
- Gestión de productos (CRUD)
- Creación de proformas con items
- Generación de PDFs
- Dashboard con estadísticas
- Base de datos SQLite

**Tecnologías:** .NET Framework/WPF, SQLite, iTextSharp (PDF)

**Ejecutar:**
1. Abrir `viveCode/MakriFormas/MakriFormas.slnx` en Visual Studio
2. Compilar y ejecutar
3. O usar el instalador en `installer/output/`

## 🛠️ Requisitos del Sistema

- **.NET SDK 10.0** o superior
- **Visual Studio 2022** o **Visual Studio Code** (recomendado para proyectos .NET)
- **Ollama** (solo para el proyecto ApiOllama)
- **SQLite** (incluido en .NET para MakriFormas)

## 📦 Instalación y Configuración

1. **Clonar el repositorio:**
```bash
git clone <url-del-repositorio>
cd API-REST_con_.NET
```

2. **Restaurar dependencias:**
```bash
dotnet restore
```

3. **Ejecutar cualquier proyecto:**
```bash
cd Basico/Api_basico_1/Api_basico_1
dotnet run
```

4. **Acceder a las APIs:**
- Swagger UI: `https://localhost:5001/swagger` (en desarrollo)
- Endpoints según cada proyecto

## 🔧 Configuración de CORS

Los proyectos de API están configurados con políticas CORS permisivas para desarrollo:
- `AllowAnyOrigin()`
- `AllowAnyHeader()`
- `AllowAnyMethod()`

**Nota:** Para producción, se recomienda restringir los orígenes permitidos.

## 📚 Aprendizajes y Buenas Prácticas

Este repositorio demuestra:

1. **Arquitectura de APIs REST** con ASP.NET Core
2. **Minimal APIs** para endpoints simples
3. **Integración con servicios externos** (Ollama)
4. **Manejo de CORS** en APIs
5. **Aplicaciones de escritorio** con WPF y SQLite
6. **Generación de PDFs** en .NET
7. **Configuración de CI/CD** con GitHub Actions

## 🤝 Contribuir

Si deseas contribuir a este proyecto:

1. Haz fork del repositorio
2. Crea una rama para tu feature (`git checkout -b feature/nueva-funcionalidad`)
3. Realiza tus cambios y commitea (`git commit -m 'Agrega nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Abre un Pull Request

## 📄 Licencia

Este proyecto está bajo la licencia MIT. Ver el archivo LICENSE para más detalles.

## 👨‍💻 Autor

**Mauricio** - [@Mfarro1522](https://github.com/Mfarro1522)

**Nota:** Este es un proyecto de aprendizaje. El código puede no seguir todas las mejores prácticas de producción y está destinado a fines educativos.
