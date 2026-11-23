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
using System.Text.RegularExpressions;

namespace ModoSofa.Agente.Win32;

/// <summary>Plan de energía (powercfg) e inhibición de la suspensión.</summary>
public sealed partial class Energia(ILogger<Energia> log) : IDisposable
{
    private Thread? _hilo;
    private ManualResetEventSlim? _liberar;

    public string? PlanActivo()
    {
        var salida = Ejecutar("/getactivescheme");
        var m = Guid().Match(salida);
        return m.Success ? m.Value : null;
    }

    public void ActivarPlan(string guid)
    {
        Ejecutar($"/setactive {guid}");
        log.LogInformation("Plan de energía: {Plan}.", guid);
    }

    /// <summary>
    /// SetThreadExecutionState solo dura mientras viva el hilo que lo pidió: se dedica un hilo que
    /// se queda esperando hasta que se libere.
    /// </summary>
    public void EvitarSuspension()
    {
        if (_hilo is not null) return;
        _liberar = new ManualResetEventSlim(false);
        var liberar = _liberar;
        _hilo = new Thread(() =>
        {
            Nativo.SetThreadExecutionState(Nativo.ES_CONTINUOUS | Nativo.ES_SYSTEM_REQUIRED | Nativo.ES_DISPLAY_REQUIRED);
            liberar.Wait();
            Nativo.SetThreadExecutionState(Nativo.ES_CONTINUOUS);
        }) { IsBackground = true, Name = "ModoSofa.SinSuspension" };
        _hilo.Start();
        log.LogInformation("Suspensión desactivada mientras dure el modo sofá.");
    }

    public void PermitirSuspension()
    {
        if (_hilo is null) return;
        _liberar!.Set();
        _hilo.Join(2000);
        _hilo = null;
        _liberar = null;
        log.LogInformation("Suspensión permitida de nuevo.");
    }

    public void Dispose() => PermitirSuspension();

    private static string Ejecutar(string argumentos)
    {
        using var p = Process.Start(new ProcessStartInfo("powercfg", argumentos)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var salida = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        if (p.ExitCode != 0) throw new InvalidOperationException($"powercfg {argumentos} devolvió {p.ExitCode}");
        return salida;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Guid();
}
