// ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
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
using CapicuaSoft.ModoSofa.Agente.Win32;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Efectos;

/// <summary>Emulador: otro emulador lanzado por ES-DE (OpenBOR…), que se trata como RetroArch.</summary>
public enum TipoActivo { Ninguno, Juego, RetroArch, Emulador, EsDe, Steam }

public sealed record Activo(TipoActivo Tipo, string Nombre, int ProcesoId, IntPtr Hwnd);

/// <summary>Steam, ES-DE, RetroArch (y los demás emuladores de ES-DE) y el juego en marcha: lanzar, enfocar y cerrar.</summary>
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

    /// <summary>Lo que debería tener el foco, por prioridad: juego, RetroArch u otro emulador, ES-DE, Steam.</summary>
    public Activo Detectar()
    {
        var lista = ventanas.DeUsuario();
        var extra = new HashSet<string>(config.Lanzadores.ProcesosNoJuego, StringComparer.OrdinalIgnoreCase);

        var juego = lista
            .Where(v => !NoJuego.Contains(v.Proceso) && !extra.Contains(v.Proceso))
            .Select(v => (v, ruta: Nativo.RutaProceso(v.ProcesoId)))
            .Where(x => EsJuego(x.v.Proceso, x.ruta))
            .OrderByDescending(x => Inicio(x.v.ProcesoId))
            .Select(x => x.v)
            .FirstOrDefault();
        if (juego is not null) return new Activo(TipoActivo.Juego, juego.Titulo, juego.ProcesoId, juego.Hwnd);

        if (lista.FirstOrDefault(v => v.Proceso.Equals(RetroArch.NombreProceso, StringComparison.OrdinalIgnoreCase)) is { } ra)
            return new Activo(TipoActivo.RetroArch, ra.Titulo, ra.ProcesoId, ra.Hwnd);

        // El más reciente: OpenBOR.exe arranca después que la pantalla de carga de LanzarOpenBor.
        if (lista.Where(v => EsEmulador(v.Proceso)).OrderByDescending(v => Inicio(v.ProcesoId)).FirstOrDefault() is { } em)
            return new Activo(TipoActivo.Emulador, em.Titulo, em.ProcesoId, em.Hwnd);

        if (lista.FirstOrDefault(v => v.Proceso.Equals(EsDe, StringComparison.OrdinalIgnoreCase)) is { } es)
            return new Activo(TipoActivo.EsDe, "ES-DE", es.ProcesoId, es.Hwnd);

        var steam = VentanaSteam();
        return steam != IntPtr.Zero
            ? new Activo(TipoActivo.Steam, "Steam", Nativo.ProcesoDeVentana(steam), steam)
            : new Activo(TipoActivo.Ninguno, "", 0, IntPtr.Zero);
    }

    /// <summary>Uno de <see cref="LanzadoresConfig.EmuladoresEsDe"/> (OpenBOR, su lanzador…).</summary>
    public bool EsEmulador(string proceso) =>
        config.Lanzadores.EmuladoresEsDe.Contains(proceso, StringComparer.OrdinalIgnoreCase);

    private bool EsJuego(string proceso, string? ruta) =>
        ruta is not null
        && !NoJuego.Contains(proceso)
        && !EsEmulador(proceso)
        && !config.Lanzadores.ProcesosNoJuego.Contains(proceso, StringComparer.OrdinalIgnoreCase)
        && config.Lanzadores.PatronesRutaJuegos.Any(p => ruta.Contains(p, StringComparison.OrdinalIgnoreCase));

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
    /// <summary>
    /// Clasifica la ventana que el usuario tiene delante (la que tenía el foco antes de abrir el
    /// menú, o la activa si la orden llega del móvil o de Alexa). Si no es ninguna de las
    /// conocidas —el panel, el propio menú, una ventana de trabajo—, se cae a <see cref="Detectar"/>.
    /// </summary>
    public Activo Delante(IntPtr ventana)
    {
        if (ventana != IntPtr.Zero && Nativo.IsWindow(ventana))
        {
            var raiz = Nativo.GetAncestor(ventana, Nativo.GA_ROOTOWNER);
            if (raiz != IntPtr.Zero) ventana = raiz;
            var pid = Nativo.ProcesoDeVentana(ventana);
            var proceso = Ventanas.NombreProceso(pid);
            var titulo = Nativo.TextoVentana(ventana);

            if (proceso.Equals(RetroArch.NombreProceso, StringComparison.OrdinalIgnoreCase))
                return new Activo(TipoActivo.RetroArch, titulo, pid, ventana);
            if (EsEmulador(proceso))
                return new Activo(TipoActivo.Emulador, titulo, pid, ventana);
            if (proceso.Equals(EsDe, StringComparison.OrdinalIgnoreCase))
                return new Activo(TipoActivo.EsDe, "ES-DE", pid, ventana);
            if (proceso is "steam" or "steamwebhelper")
                return new Activo(TipoActivo.Steam, "Steam", pid, ventana);
            if (EsJuego(proceso, Nativo.RutaProceso(pid)))
                return new Activo(TipoActivo.Juego, titulo, pid, ventana);
        }
        return Detectar();
    }

    /// <summary>
    /// Cierra lo que hay delante y devuelve el foco al nivel anterior:
    /// RetroArch u otro emulador → ES-DE (o Steam); ES-DE → Steam; juego de Steam → Steam.
    /// Un juego de Steam no guarda al cerrarlo a la fuerza: exige <paramref name="confirmado"/>.
    /// </summary>
    public async Task CerrarJuegoAsync(Activo objetivo, bool confirmado)
    {
        switch (objetivo.Tipo)
        {
            case TipoActivo.RetroArch:
                log.LogInformation("Cerrando RetroArch: {Nombre}.", objetivo.Nombre);
                if (!await retroArch.SalirAsync(TimeSpan.FromSeconds(5)))
                    await CerrarProcesoAsync(objetivo.ProcesoId, objetivo.Hwnd, TimeSpan.FromSeconds(4));
                break;
            case TipoActivo.Emulador:
                // OpenBOR no tiene comandos de red: WM_CLOSE (SDL lo recibe como salir) y, si no
                // basta, se termina el árbol. Al cerrar OpenBOR.exe, LanzarOpenBor termina solo.
                log.LogInformation("Cerrando el emulador: {Nombre}.", objetivo.Nombre);
                await CerrarProcesoAsync(objetivo.ProcesoId, objetivo.Hwnd, TimeSpan.FromSeconds(5));
                break;
            case TipoActivo.EsDe:
                log.LogInformation("Cerrando ES-DE.");
                await CerrarProcesoAsync(objetivo.ProcesoId, objetivo.Hwnd, TimeSpan.FromSeconds(5));
                break;
            case TipoActivo.Juego when !confirmado:
                throw new InvalidOperationException($"Cerrar «{objetivo.Nombre}» puede perder lo no guardado: hay que confirmarlo.");
            case TipoActivo.Juego:
                log.LogInformation("Cerrando el juego: {Nombre}.", objetivo.Nombre);
                await CerrarProcesoAsync(objetivo.ProcesoId, objetivo.Hwnd, TimeSpan.FromSeconds(5));
                break;
            default:
                log.LogInformation("No hay ningún juego delante que cerrar.");
                return;
        }

        await Task.Delay(500);
        var esde = Process.GetProcessesByName(EsDe).FirstOrDefault();
        var destino = esde is not null && objetivo.Tipo is TipoActivo.RetroArch or TipoActivo.Emulador ? ventanas.VentanaDe(esde.Id) : VentanaSteam();
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
