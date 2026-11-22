using System.Net;
using System.Net.Sockets;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Efectos;

public static class WakeOnLan
{
    /// <summary>Envía el paquete mágico por difusión al puerto 9 (tres veces: es UDP).</summary>
    public static async Task EnviarAsync(string mac)
    {
        var paquete = WakeOnLanPaquete.Crear(mac);
        using var udp = new UdpClient { EnableBroadcast = true };
        for (var i = 0; i < 3; i++)
            await udp.SendAsync(paquete, paquete.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }
}
