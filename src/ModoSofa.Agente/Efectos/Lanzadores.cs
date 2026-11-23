// ModoSofá - Convierte el PC de trabajo en una consola de salón.
// Copyright (C) 2026  José Luis Bautista Martín
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

using System.Diagnostics;
using Microsoft.Win32;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Efectos;

public enum TipoActivo { Ninguno, Juego, RetroArch, EsDe, Steam }

public sealed record Activo(TipoActivo Tipo, string Nombre, int ProcesoId, IntPtr Hwnd);

/// <summary>Steam, ES-DE, RetroArch y el juego en marcha: lanzar, enfocar y cerrar.</summary>
public sealed class Lanzadores(Configuracion config, Ventanas ventanas, RetroArch retroArch, ILogger<Lanzadores> log)
{
    private const string EsDe = "ES-DE";

    /// <summary>Procesos que nunca son «el juego» aunque estén bajo una ruta de juegos.</summary>
    private static readonly HashSet<string> NoJuego = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamwebhelper", "steamservice", "gameoverlayui", "gameoverlayui64", "GameOverlayUI",
        EsDe, RetroArch.NombreProceso, "ModoSofa", "UnityCrashHandler64", "UnityCrashHandler32",
        "CrashReportClient", "EpicWebHelper", "EADesktop", "EABackgroundService", "upc", "UbisoftConnect",
        "explorer", "dwm", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost",
    };

    // --- Steam -----------------------------------------------------------------------------

    public string? RutaSteam()
    {
        if (!string.IsNullOrWhiteSpace(config.Lanzadores.SteamExe)) return config.Lanzadores.SteamExe;
        var valor = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) as string;
        return valor?.Replace('/', '\\');
    }

    public static bool SteamAbierto() => Process.GetProcessesByName("steam").Length > 0;

    /// <summary>Abre Steam en Big Picture (o cambia a Big Picture si ya estaba abierto) y espera su ventana.</summary>
    public async Task BigPictureAsync(CancellationToken ct = default)
    {
        // Lanzar steam.exe tarda ~15 s en este equipo (antes incluso de que el proceso arranque),
        // así que si Big Picture ya está abierto basta con darle el foco.
        var abierta = VentanaBigPicture();
        if (abierta != IntPtr.Zero)
        {
            ventanas.Enfocar(abierta);
            log.LogInformation("Big Picture ya estaba abierto: foco devuelto.");
            return;
        }

        var ruta = RutaSteam() ?? throw new InvalidOperationException("No se encuentra steam.exe (configura Lanzadores.SteamExe).");
        if (SteamAbierto())
        {
            // Se pasa el enlace a steam.exe, que lo entrega a la instancia abierta. Abrirlo con
            // el shell (UseShellExecute) bloqueaba ~15 s.
            Process.Start(new ProcessStartInfo(ruta, "steam://open/bigpicture") { UseShellExecute = false });
            log.LogInformation("Steam ya estaba abierto: cambiando a Big Picture.");
        }
        else
        {
            Process.Start(new ProcessStartInfo(ruta, "-gamepadui") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(ruta)! });
            log.LogInformation("Iniciando Steam en Big Picture.");
        }

        var hwnd = await EsperarAsync(VentanaBigPicture, TimeSpan.FromSeconds(90), ct);
        if (hwnd == IntPtr.Zero)
        {
            log.LogWarning("Big Picture no apareció en 90 s.");
            return;
        }
        ventanas.Enfocar(hwnd);
        log.LogInformation("Big Picture listo.");
    }

    public IntPtr VentanaBigPicture() =>
        ventanas.DeUsuario()
            .Where(v => v.Proceso.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase)
                     || v.Proceso.Equals("steam", StringComparison.OrdinalIgnoreCase))
            .Where(v => v.Titulo.Contains("Big Picture", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Hwnd)
            .FirstOrDefault();

    public IntPtr VentanaSteam() =>
        VentanaBigPicture() is var bp && bp != IntPtr.Zero ? bp
        : ventanas.DeUsuario()
            .Where(v => v.Proceso.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase)
                     || v.Proceso.Equals("steam", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Hwnd)
            .FirstOrDefault();

    /// <summary>Cierre ordenado con «steam -shutdown»; si no basta, se termina el árbol de procesos.</summary>
    public async Task CerrarSteamAsync()
    {
        if (!SteamAbierto()) return;
        var ruta = RutaSteam();
        if (ruta is not null)
            Process.Start(new ProcessStartInfo(ruta, "-shutdown") { UseShellExecute = false });

        for (var i = 0; i < 40 && SteamAbierto(); i++) await Task.Delay(500);

        if (SteamAbierto())
        {
            log.LogWarning("Steam no se cerró en 20 s; se termina el proceso.");
            foreach (var nombre in new[] { "steam", "steamwebhelper" })
                foreach (var p in Process.GetProcessesByName(nombre))
                    try { p.Kill(entireProcessTree: true); } catch { /* ya cerrado */ }
        }
        log.LogInformation("Steam cerrado.");
    }

    // --- ES-DE -----------------------------------------------------------------------------

    public async Task AbrirEsDeAsync(CancellationToken ct = default)
    {
        var abierto = Process.GetProcessesByName(EsDe).FirstOrDefault();
        if (abierto is not null)
        {
            ventanas.Enfocar(ventanas.VentanaDe(abierto.Id));
            return;
        }
        var ruta = config.Lanzadores.EsdeExe;
        if (!File.Exists(ruta)) throw new FileNotFoundException("No se encuentra ES-DE.", ruta);
        var p = Process.Start(new ProcessStartInfo(ruta) { WorkingDirectory = Path.GetDirectoryName(ruta)!, UseShellExecute = false })!;
        var hwnd = await EsperarAsync(() => ventanas.VentanaDe(p.Id), TimeSpan.FromSeconds(30), ct);
        ventanas.Enfocar(hwnd);
        log.LogInformation("ES-DE abierto.");
    }

    // --- Juego activo ----------------------------------------------------------------------

    /// <summary>Lo que debería tener el foco, por prioridad: juego, RetroArch, ES-DE, Steam.</summary>
    public Activo Detectar()
    {
        var lista = ventanas.DeUsuario();
        var extra = new HashSet<string>(config.Lanzadores.ProcesosNoJuego, StringComparer.OrdinalIgnoreCase);

        var juego = lista
            .Where(v => !NoJuego.Contains(v.Proceso) && !extra.Contains(v.Proceso))
            .Select(v => (v, ruta: Nativo.RutaProceso(v.ProcesoId)))
            .Where(x => x.ruta is not null && config.Lanzadores.PatronesRutaJuegos
                .Any(p => x.ruta.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => Inicio(x.v.ProcesoId))
            .Select(x => x.v)
            .FirstOrDefault();
        if (juego is not null) return new Activo(TipoActivo.Juego, juego.Titulo, juego.ProcesoId, juego.Hwnd);

        if (lista.FirstOrDefault(v => v.Proceso.Equals(RetroArch.NombreProceso, StringComparison.OrdinalIgnoreCase)) is { } ra)
            return new Activo(TipoActivo.RetroArch, ra.Titulo, ra.ProcesoId, ra.Hwnd);

        if (lista.FirstOrDefault(v => v.Proceso.Equals(EsDe, StringComparison.OrdinalIgnoreCase)) is { } es)
            return new Activo(TipoActivo.EsDe, "ES-DE", es.ProcesoId, es.Hwnd);

        var steam = VentanaSteam();
        return steam != IntPtr.Zero
            ? new Activo(TipoActivo.Steam, "Steam", Nativo.ProcesoDeVentana(steam), steam)
            : new Activo(TipoActivo.Ninguno, "", 0, IntPtr.Zero);
    }

    public void EnfocarActivo()
    {
        var activo = Detectar();
        if (activo.Tipo == TipoActivo.Ninguno)
        {
            log.LogInformation("No hay nada que enfocar.");
            return;
        }
        ventanas.Enfocar(activo.Hwnd);
        log.LogInformation("Foco en {Tipo}: {Nombre}.", activo.Tipo, activo.Nombre);
    }

    /// <summary>
    /// Cierra lo que haya en primer plano del juego, de forma escalonada (comando de red o WM_CLOSE,
    /// luego terminar el proceso) y devuelve el foco a ES-DE o a Steam.
    /// </summary>
    public async Task CerrarJuegoAsync()
    {
        var activo = Detectar();
        switch (activo.Tipo)
        {
            case TipoActivo.Juego:
                log.LogInformation("Cerrando el juego: {Nombre}.", activo.Nombre);
                await CerrarProcesoAsync(activo.ProcesoId, activo.Hwnd, TimeSpan.FromSeconds(5));
                break;
            case TipoActivo.RetroArch:
                log.LogInformation("Cerrando RetroArch: {Nombre}.", activo.Nombre);
                if (!await retroArch.SalirAsync(TimeSpan.FromSeconds(5)))
                    await CerrarProcesoAsync(activo.ProcesoId, activo.Hwnd, TimeSpan.FromSeconds(4));
                break;
            default:
                log.LogInformation("No hay ningún juego en marcha.");
                break;
        }

        await Task.Delay(500);
        var esde = Process.GetProcessesByName(EsDe).FirstOrDefault();
        var destino = esde is not null ? ventanas.VentanaDe(esde.Id) : VentanaSteam();
        ventanas.Enfocar(destino);
    }

    private async Task CerrarProcesoAsync(int pid, IntPtr hwnd, TimeSpan espera)
    {
        Process proceso;
        try { proceso = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; }

        ventanas.Cerrar(hwnd);
        if (await EsperarSalidaAsync(proceso, espera)) return;

        log.LogWarning("{Proceso} no se cerró; se termina.", proceso.ProcessName);
        try { proceso.Kill(entireProcessTree: true); } catch { /* ya cerrado */ }
    }

    private static async Task<bool> EsperarSalidaAsync(Process p, TimeSpan espera)
    {
        using var cts = new CancellationTokenSource(espera);
        try { await p.WaitForExitAsync(cts.Token); return true; }
        catch (OperationCanceledException) { return p.HasExited; }
    }

    private static DateTime Inicio(int pid)
    {
        try { return Process.GetProcessById(pid).StartTime; }
        catch { return DateTime.MinValue; }
    }

    private static async Task<IntPtr> EsperarAsync(Func<IntPtr> buscar, TimeSpan limite, CancellationToken ct)
    {
        var fin = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fin && !ct.IsCancellationRequested)
        {
            var h = buscar();
            if (h != IntPtr.Zero) return h;
            await Task.Delay(500, ct);
        }
        return IntPtr.Zero;
    }
}
