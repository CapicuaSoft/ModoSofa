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
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Win32;

/// <summary>Lectura del mando con XInput. Funciona en segundo plano: no necesita el foco.</summary>
internal static class XInput
{
    public const int MaxMandos = 4;
    private const int ERROR_SUCCESS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int indice, out XINPUT_STATE estado);

    /// <summary>Botones pulsados, o null si no hay mando en esa ranura.</summary>
    public static Botones? Leer(int indice) =>
        XInputGetState(indice, out var estado) == ERROR_SUCCESS ? (Botones)estado.Gamepad.wButtons : null;
}
