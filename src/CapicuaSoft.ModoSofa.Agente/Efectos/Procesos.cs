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
using CapicuaSoft.ModoSofa.Agente.Win32;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Efectos;

/// <summary>Cierra los programas de trabajo de la lista y los vuelve a abrir al salir.</summary>
public sealed class Procesos(Configuracion config, Ventanas ventanas, ILogger<Procesos> log)
{
    public async Task<List<ProcesoCerrado>> CerrarListaAsync()
    {
        var cerrados = new List<ProcesoCerrado>();
        var vivos = new List<Process>();
        // Procesos con alguna ventana visible: los que no tienen ninguna están solo en la bandeja.
        var conVentana = ventanas.DeUsuario().Select(v => v.ProcesoId).ToHashSet();

        foreach (var nombre in config.Ventanas.CerrarProcesos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var procesos = Process.GetProcessesByName(nombre);
            if (procesos.Length == 0) continue;

            // Se guarda una sola entrada por programa, aunque tenga varios procesos.
            var principal = procesos.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? procesos[0];
            var visible = procesos.Any(p => conVentana.Contains(p.Id));
            cerrados.Add(new ProcesoCerrado(nombre, Nativo.RutaProceso(principal.Id), Nativo.AumidProceso(principal.Id), visible));

            foreach (var p in procesos)
            {
                try { p.CloseMainWindow(); } catch { /* sin ventana */ }
                vivos.Add(p);
            }
            log.LogInformation("Cerrando {Nombre}{Bandeja}.", nombre, visible ? "" : " (estaba en la bandeja)");
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
                if (!p.ConVentana && ArranqueSegundoPlano.Enlace(p.Nombre) is { } enlace)
                {
                    Process.Start(new ProcessStartInfo(enlace) { UseShellExecute = true });
                    log.LogInformation("Relanzado {Nombre} en segundo plano.", p.Nombre);
                    continue;
                }
                if (p.Aumid is not null)
                    // Las apps MSIX (Teams nuevo, Outlook nuevo) no se pueden lanzar por su exe.
                    Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{p.Aumid}") { UseShellExecute = false });
                else if (p.Ruta is not null)
                    Process.Start(new ProcessStartInfo(p.Ruta) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p.Ruta)! });
                else
                    continue;
                log.LogInformation("Relanzado {Nombre}.", p.Nombre);
                if (!p.ConVentana) _ = MinimizarAlAparecerAsync(p.Nombre);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "No se pudo relanzar {Nombre}.", p.Nombre);
            }
        }
    }

    /// <summary>
    /// Para un programa que estaba en la bandeja y no sabemos arrancar sin ventana: minimiza su
    /// ventana cuando aparece. Minimizar es seguro; cerrarla podría terminar el programa.
    /// </summary>
    private async Task MinimizarAlAparecerAsync(string nombre)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < limite)
        {
            await Task.Delay(500);
            var ventana = ventanas.DeUsuario().FirstOrDefault(v => v.Proceso.Equals(nombre, StringComparison.OrdinalIgnoreCase));
            if (ventana is null) continue;
            ventanas.Minimizar(ventana.Hwnd);
            log.LogInformation("{Nombre} estaba en la bandeja: se minimiza su ventana.", nombre);
            return;
        }
    }

    private static bool Salio(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }
}
