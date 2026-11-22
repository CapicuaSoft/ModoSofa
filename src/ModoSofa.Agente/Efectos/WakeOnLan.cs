using System.Net;
using System.Net.Sockets;

namespace ModoSofa.Agente.Efectos;

public static class WakeOnLan
{
    /// <summary>Paquete mágico: 6 × 0xFF y 16 × la MAC, por difusión al puerto 9.</summary>
    public static async Task EnviarAsync(string mac)
    {
        var bytes = Convert.FromHexString(mac.Replace(":", "").Replace("-", "").Trim());
        if (bytes.Length != 6) throw new FormatException($"MAC no válida: {mac}");

        var paquete = new byte[102];
        for (var i = 0; i < 6; i++) paquete[i] = 0xFF;
        for (var i = 1; i <= 16; i++) Buffer.BlockCopy(bytes, 0, paquete, i * 6, 6);

        using var udp = new UdpClient { EnableBroadcast = true };
        await udp.SendAsync(paquete, paquete.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }
}
