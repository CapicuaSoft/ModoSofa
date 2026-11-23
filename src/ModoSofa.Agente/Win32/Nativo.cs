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

using System.Runtime.InteropServices;
using System.Text;

namespace ModoSofa.Agente.Win32;

/// <summary>P/Invoke de user32, kernel32 y dwmapi que usan los efectos.</summary>
internal static partial class Nativo
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder texto, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder texto, int max);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
    [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int valor, int tamano);

    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint acceso, bool heredar, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr proceso, uint flags, StringBuilder ruta, ref uint tamano);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetApplicationUserModelId(IntPtr proceso, ref uint longitud, StringBuilder aumid);

    public const uint GW_OWNER = 4;
    public const uint GA_ROOTOWNER = 3;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int DWMWA_CLOAKED = 14;
    public const int SW_RESTORE = 9;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const uint WM_CLOSE = 0x0010;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_SHOWWINDOW = 0x40, SWP_NOACTIVATE = 0x10;
    public const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const byte VK_MENU = 0x12;
    public const uint KEYEVENTF_KEYUP = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    /// <summary>
    /// Pone una ventana delante con el foco. Windows solo deja robar el foco al proceso que tuvo la
    /// última entrada: pulsar y soltar Alt levanta esa restricción. El TOPMOST/NOTOPMOST la sube
    /// por encima de otras ventanas «siempre encima» sin dejarla fijada ahí.
    /// </summary>
    public static void TraerAlFrente(IntPtr hwnd, bool dejarEncima = false)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        if (!dejarEncima) SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }

    public static string TextoVentana(IntPtr hwnd)
    {
        var largo = GetWindowTextLength(hwnd);
        if (largo == 0) return "";
        var sb = new StringBuilder(largo + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        // Steam en español titula «Modo Big Picture de Steam» con un espacio no separable.
        return sb.ToString().Replace(' ', ' ');
    }

    public static string ClaseVentana(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static int ProcesoDeVentana(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    /// <summary>Ruta del ejecutable sin pedir más permisos que los imprescindibles.</summary>
    public static string? RutaProceso(int pid)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint tamano = (uint)sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref tamano) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>AUMID de una app empaquetada (MSIX), o null si es una app de escritorio normal.</summary>
    public static string? AumidProceso(int pid)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            uint largo = 512;
            var sb = new StringBuilder((int)largo);
            return GetApplicationUserModelId(h, ref largo, sb) == 0 ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }
}
