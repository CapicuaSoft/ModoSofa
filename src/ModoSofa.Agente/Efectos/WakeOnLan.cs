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
