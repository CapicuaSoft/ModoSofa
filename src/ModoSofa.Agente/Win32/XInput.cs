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
