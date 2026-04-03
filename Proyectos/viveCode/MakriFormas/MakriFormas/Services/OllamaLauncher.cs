using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    /// <summary>
    /// Gestiona el ciclo de vida de Ollama de forma transparente para el usuario.
    /// Al iniciar la app: comprueba si Ollama corre → si no, lo lanza.
    /// Si falla → mata todos los procesos de Ollama y reintenta una vez.
    /// </summary>
    public static class OllamaLauncher
    {
        private const string OllamaApiUrl   = "http://localhost:11434/api/version";
        private const string OllamaExeName  = "ollama";
        private const int    StartupWaitMs  = 6000;   // tiempo máximo de espera al arrancar
        private const int    PingTimeoutMs  = 3000;
        private const int    MaxRetries     = 2;

        // ── Entry point principal ─────────────────────────────────────────────

        /// <summary>
        /// Llama a este método en App.OnStartup (en background, sin bloquear la UI).
        /// </summary>
        public static async Task EnsureRunningAsync(CancellationToken ct = default)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                // 1. ¿Ya está corriendo?
                if (await IsPingOkAsync(ct))
                    return;

                // 2. Intentar lanzar
                if (attempt == 1)
                {
                    TryLaunchOllama();
                    await Task.Delay(StartupWaitMs, ct);

                    if (await IsPingOkAsync(ct))
                        return;
                }

                // 3. Algo salió mal — matar todos los procesos y reintentar
                if (attempt < MaxRetries)
                {
                    KillAllOllamaProcesses();
                    await Task.Delay(1500, ct);
                    TryLaunchOllama();
                    await Task.Delay(StartupWaitMs, ct);
                }
            }

            // Después de todos los intentos, no hacemos nada más:
            // la app funciona sin IA, y AgentChatWindow mostrará el aviso.
        }

        // ── Comprobación de salud ─────────────────────────────────────────────

        public static async Task<bool> IsPingOkAsync(CancellationToken ct = default)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(PingTimeoutMs) };
                var resp = await http.GetAsync(OllamaApiUrl, ct);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ── Lanzamiento ───────────────────────────────────────────────────────

        private static void TryLaunchOllama()
        {
            // Buscar ollama.exe en rutas conocidas
            var exePath = FindOllamaExe();
            if (exePath == null)
                return; // No instalado — nada que hacer

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = exePath,
                    Arguments              = "serve",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,   // sin ventana de consola
                    WindowStyle            = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = false,
                    RedirectStandardError  = false,
                };

                Process.Start(psi);
            }
            catch
            {
                // No se pudo lanzar — continuamos sin IA
            }
        }

        // ── Kill ──────────────────────────────────────────────────────────────

        private static void KillAllOllamaProcesses()
        {
            try
            {
                var procs = Process.GetProcessesByName(OllamaExeName);
                foreach (var p in procs)
                {
                    try { p.Kill(entireProcessTree: true); } catch { /* ignorar */ }
                    p.Dispose();
                }
            }
            catch { /* ignorar errores de permisos */ }
        }

        // ── Búsqueda del ejecutable ───────────────────────────────────────────

        private static string? FindOllamaExe()
        {
            // 1. ¿Está en el PATH del sistema?
            var inPath = FindInPath("ollama.exe");
            if (inPath != null) return inPath;

            // 2. Rutas de instalación típicas de Ollama en Windows
            var candidates = new[]
            {
                // Instalador oficial de Ollama en Windows
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             "Programs", "Ollama", "ollama.exe"),
                // Alternativa portable / chocolatey
                @"C:\Program Files\Ollama\ollama.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                             "AppData", "Local", "Ollama", "ollama.exe"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string? FindInPath(string exeName)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    var full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch { /* directorio inaccesible */ }
            }
            return null;
        }
    }
}
