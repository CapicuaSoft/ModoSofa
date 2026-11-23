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

namespace CapicuaSoft.ModoSofa.Nucleo;

/// <summary>Formatos binarios y de texto de terceros, separados de la E/S para poder probarlos.</summary>
public static class Edid
{
    /// <summary>
    /// Código de fabricante PnP (3 letras de 5 bits) tal como lo devuelve DisplayConfigGetDeviceInfo:
    /// los bytes del EDID (big-endian) leídos como ushort little-endian. Las LG anuncian «GSM».
    /// </summary>
    public static string Fabricante(ushort id)
    {
        var v = (ushort)((id >> 8) | (id << 8));
        if (v == 0) return "?";
        return new string([Letra(v >> 10), Letra(v >> 5), Letra(v)]);

        static char Letra(int valor) => (char)('A' - 1 + (valor & 31));
    }
}

public static class WakeOnLanPaquete
{
    /// <summary>Paquete mágico: 6 × 0xFF seguidos de 16 repeticiones de la MAC (102 bytes).</summary>
    public static byte[] Crear(string mac)
    {
        var limpia = mac.Replace(":", "").Replace("-", "").Replace(".", "").Trim();
        byte[] bytes;
        try { bytes = Convert.FromHexString(limpia); }
        catch (FormatException) { throw new FormatException($"MAC no válida: {mac}"); }
        if (bytes.Length != 6) throw new FormatException($"MAC no válida: {mac}");

        var paquete = new byte[102];
        Array.Fill(paquete, (byte)0xFF, 0, 6);
        for (var i = 1; i <= 16; i++) Buffer.BlockCopy(bytes, 0, paquete, i * 6, 6);
        return paquete;
    }
}

public static class RetroArchProtocolo
{
    /// <summary>
    /// "GET_STATUS PLAYING snes,Juego,crc32=…" → "PLAYING". También "PAUSED" y "CONTENTLESS".
    /// Null si la respuesta no es de GET_STATUS.
    /// </summary>
    public static string? EstadoDe(string respuesta)
    {
        var partes = respuesta.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 2 && partes[0] == "GET_STATUS" ? partes[1] : null;
    }
}

/// <summary>
/// Teles LG NetCast (2012–2014, p. ej. la 50LB5830): API UDAP por HTTP en el puerto 8080. Se
/// vinculan con un código de 6 cifras que la tele muestra en pantalla, y después aceptan
/// pulsaciones de su mando a distancia.
/// </summary>
public static class NetCastProtocolo
{
    /// <summary>Teclas del mando (HandleKeyInput) que usa ModoSofá.</summary>
    public enum Tecla { Encendido = 1, SubirVolumen = 24, BajarVolumen = 25, Silenciar = 26, Entradas = 47 }

    public const string AgenteDeUsuario = "UDAP/2.0";

    public static string PedirCodigo() =>
        """<?xml version="1.0" encoding="utf-8"?><envelope><api type="pairing"><name>showKey</name></api></envelope>""";

    /// <summary>Saludo con el código: vincula la primera vez y abre sesión las siguientes.</summary>
    public static string Saludo(string codigo, int puerto = 8080)
    {
        var limpio = new string(codigo.Where(char.IsAsciiDigit).ToArray());
        if (limpio.Length == 0) throw new FormatException("El código de la tele son números.");
        return $"""<?xml version="1.0" encoding="utf-8"?><envelope><api type="pairing"><name>hello</name><value>{limpio}</value><port>{puerto}</port></api></envelope>""";
    }

    public static string Pulsar(Tecla tecla) =>
        $"""<?xml version="1.0" encoding="utf-8"?><envelope><api type="command"><name>HandleKeyInput</name><value>{(int)tecla}</value></api></envelope>""";

    /// <summary>La respuesta a «netrcu.model» lleva el modelo: así se reconoce una NetCast.</summary>
    public static string? Modelo(string respuesta)
    {
        var i = respuesta.IndexOf("<modelName>", StringComparison.Ordinal);
        var j = respuesta.IndexOf("</modelName>", StringComparison.Ordinal);
        return i >= 0 && j > i ? respuesta[(i + "<modelName>".Length)..j].Trim() : null;
    }
}
