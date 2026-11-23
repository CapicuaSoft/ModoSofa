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
using System.Runtime.InteropServices;

namespace CapicuaSoft.ModoSofa.Agente.Win32;

public sealed record VentanaInfo(IntPtr Hwnd, int ProcesoId, string Proceso, string Titulo);

/// <summary>Ventanas de usuario: enumerar, minimizar recordando su posición, restaurar y enfocar.</summary>
public sealed class Ventanas(ILogger<Ventanas> log)
{
    /// <summary>Clases del escritorio, barra de tareas y menú Inicio: nunca se tocan.</summary>
    private static readonly HashSet<string> ClasesShell = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow",
    };

    private static readonly int PropioPid = Environment.ProcessId;

    /// <summary>Ventanas de primer nivel visibles, con título, sin propietario y no ocultas por DWM.</summary>
    public List<VentanaInfo> DeUsuario()
    {
        var lista = new List<VentanaInfo>();
        Nativo.EnumWindows((hwnd, _) =>
        {
            if (EsDeUsuario(hwnd))
            {
                var pid = Nativo.ProcesoDeVentana(hwnd);
                if (pid != PropioPid)
                    lista.Add(new VentanaInfo(hwnd, pid, NombreProceso(pid), Nativo.TextoVentana(hwnd)));
            }
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    public static bool EsDeUsuario(IntPtr hwnd)
    {
        if (!Nativo.IsWindowVisible(hwnd)) return false;
        if (Nativo.GetWindow(hwnd, Nativo.GW_OWNER) != IntPtr.Zero) return false;
        if (Nativo.GetWindowTextLength(hwnd) == 0) return false;
        if ((Nativo.GetWindowLong(hwnd, Nativo.GWL_EXSTYLE) & Nativo.WS_EX_TOOLWINDOW) != 0) return false;
        if (Nativo.DwmGetWindowAttribute(hwnd, Nativo.DWMWA_CLOAKED, out var oculta, sizeof(int)) == 0 && oculta != 0) return false;
        return !ClasesShell.Contains(Nativo.ClaseVentana(hwnd));
    }

    public static string NombreProceso(int pid)
    {
        try { return Process.GetProcessById(pid).ProcessName; }
        catch { return "?"; }
    }

    public static string GuardarPosicion(IntPtr hwnd)
    {
        var wp = new Nativo.WINDOWPLACEMENT { length = Marshal.SizeOf<Nativo.WINDOWPLACEMENT>() };
        Nativo.GetWindowPlacement(hwnd, ref wp);
        return Convert.ToBase64String(MemoryMarshal.AsBytes(new Span<Nativo.WINDOWPLACEMENT>(ref wp)).ToArray());
    }

    public void Minimizar(IntPtr hwnd) => Nativo.ShowWindowAsync(hwnd, Nativo.SW_SHOWMINNOACTIVE);

    /// <summary>Restaura la posición guardada si la ventana sigue siendo la misma (mismo proceso).</summary>
    public bool Restaurar(long hwnd, int pid, string posicion)
    {
        var h = new IntPtr(hwnd);
        if (!Nativo.IsWindow(h) || Nativo.ProcesoDeVentana(h) != pid) return false;
        var wp = MemoryMarshal.Read<Nativo.WINDOWPLACEMENT>(Convert.FromBase64String(posicion));
        wp.length = Marshal.SizeOf<Nativo.WINDOWPLACEMENT>();
        // showCmd minimizado se respeta: una ventana que ya estaba minimizada sigue así.
        return Nativo.SetWindowPlacement(h, ref wp);
    }

    /// <summary>
    /// Trae una ventana al frente en cualquier monitor. Windows solo deja robar el foco al proceso
    /// que tuvo la última entrada; pulsar y soltar Alt levanta esa restricción.
    /// </summary>
    public void Enfocar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Nativo.IsWindow(hwnd)) return;
        if (Nativo.IsIconic(hwnd)) Nativo.ShowWindow(hwnd, Nativo.SW_RESTORE);

        Nativo.keybd_event(Nativo.VK_MENU, 0, 0, UIntPtr.Zero);
        Nativo.keybd_event(Nativo.VK_MENU, 0, Nativo.KEYEVENTF_KEYUP, UIntPtr.Zero);

        Nativo.SetWindowPos(hwnd, Nativo.HWND_TOPMOST, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_SHOWWINDOW);
        Nativo.SetWindowPos(hwnd, Nativo.HWND_NOTOPMOST, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_SHOWWINDOW);
        Nativo.BringWindowToTop(hwnd);
        if (!Nativo.SetForegroundWindow(hwnd))
            log.LogDebug("SetForegroundWindow rechazado para {Titulo}.", Nativo.TextoVentana(hwnd));
    }

    /// <summary>Ventana principal visible de un proceso (la de mayor área si hay varias).</summary>
    public IntPtr VentanaDe(int pid) =>
        DeUsuario().Where(v => v.ProcesoId == pid).Select(v => v.Hwnd).FirstOrDefault();

    public void Cerrar(IntPtr hwnd) => Nativo.PostMessage(hwnd, Nativo.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
}
