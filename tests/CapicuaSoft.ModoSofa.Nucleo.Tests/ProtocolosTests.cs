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

using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Nucleo.Tests;

public class EdidTests
{
    // DisplayConfigGetDeviceInfo devuelve los bytes del EDID (big-endian) leídos como little-endian.
    [Theory]
    [InlineData((ushort)0xEF04, "AGO")] // el monitor de este PC (comprobado con WmiMonitorID)
    [InlineData((ushort)0x6D1E, "GSM")] // LG Electronics
    [InlineData((ushort)0x2D4C, "SAM")] // Samsung
    public void Decodifica_el_fabricante(ushort id, string esperado)
    {
        Assert.Equal(esperado, Edid.Fabricante(id));
    }

    [Fact]
    public void Sin_fabricante()
    {
        Assert.Equal("?", Edid.Fabricante(0));
    }
}

public class WakeOnLanTests
{
    [Theory]
    [InlineData("02:11:22:33:44:55")]
    [InlineData("02-11-22-33-44-55")]
    [InlineData("021122334455")]
    public void Paquete_magico(string mac)
    {
        var p = WakeOnLanPaquete.Crear(mac);
        Assert.Equal(102, p.Length);
        Assert.All(p[..6], b => Assert.Equal(0xFF, b));
        byte[] esperada = [0x02, 0x11, 0x22, 0x33, 0x44, 0x55];
        for (var i = 1; i <= 16; i++) Assert.Equal(esperada, p[(i * 6)..(i * 6 + 6)]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("02:11:22:33:44")]
    [InlineData("ZZ:11:22:33:44:55")]
    public void Rechaza_MAC_no_validas(string mac)
    {
        Assert.Throws<FormatException>(() => WakeOnLanPaquete.Crear(mac));
    }
}

public class RetroArchProtocoloTests
{
    [Theory]
    [InlineData("GET_STATUS PLAYING snes,Super Mario World,crc32=b19ed489\n", "PLAYING")]
    [InlineData("GET_STATUS PAUSED snes,Juego,crc32=0", "PAUSED")]
    [InlineData("GET_STATUS CONTENTLESS", "CONTENTLESS")]
    public void Lee_el_estado(string respuesta, string esperado)
    {
        Assert.Equal(esperado, RetroArchProtocolo.EstadoDe(respuesta));
    }

    [Theory]
    [InlineData("")]
    [InlineData("GET_STATUS")]
    [InlineData("VERSION 1.22.2")]
    public void Ignora_respuestas_ajenas(string respuesta)
    {
        Assert.Null(RetroArchProtocolo.EstadoDe(respuesta));
    }
}
