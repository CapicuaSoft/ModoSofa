using System.Diagnostics;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Efectos;

/// <summary>Cierra los programas de trabajo de la lista y los vuelve a abrir al salir.</summary>
public sealed class Procesos(Configuracion config, ILogger<Procesos> log)
{
    public async Task<List<ProcesoCerrado>> CerrarListaAsync()
    {
        var cerrados = new List<ProcesoCerrado>();
        var vivos = new List<Process>();

        foreach (var nombre in config.Ventanas.CerrarProcesos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var procesos = Process.GetProcessesByName(nombre);
            if (procesos.Length == 0) continue;

            // Se guarda una sola entrada por programa, aunque tenga varios procesos.
            var principal = procesos.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? procesos[0];
            cerrados.Add(new ProcesoCerrado(nombre, Nativo.RutaProceso(principal.Id), Nativo.AumidProceso(principal.Id)));

            foreach (var p in procesos)
            {
                try { p.CloseMainWindow(); } catch { /* sin ventana */ }
                vivos.Add(p);
            }
            log.LogInformation("Cerrando {Nombre}.", nombre);
        }

        if (vivos.Count == 0) return cerrados;

        var limite = DateTime.UtcNow.AddMilliseconds(config.Ventanas.EsperaCierreMs);
        while (DateTime.UtcNow < limite && vivos.Any(p => !Salio(p))) await Task.Delay(250);

        if (config.Ventanas.ForzarCierre)
        {
            foreach (var p in vivos.Where(p => !Salio(p)))
            {
                log.LogInformation("{Nombre} sigue abierto; se termina.", p.ProcessName);
                try { p.Kill(entireProcessTree: true); } catch { /* ya cerrado */ }
            }
        }
        return cerrados;
    }

    public void Relanzar(IEnumerable<ProcesoCerrado> procesos)
    {
        foreach (var p in procesos)
        {
            if (Process.GetProcessesByName(p.Nombre).Length > 0) continue;
            try
            {
                if (p.Aumid is not null)
                    // Las apps MSIX (Teams nuevo, Outlook nuevo) no se pueden lanzar por su exe.
                    Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{p.Aumid}") { UseShellExecute = false });
                else if (p.Ruta is not null)
                    Process.Start(new ProcessStartInfo(p.Ruta) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p.Ruta)! });
                else
                    continue;
                log.LogInformation("Relanzado {Nombre}.", p.Nombre);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "No se pudo relanzar {Nombre}.", p.Nombre);
            }
        }
    }

    private static bool Salio(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }
}
