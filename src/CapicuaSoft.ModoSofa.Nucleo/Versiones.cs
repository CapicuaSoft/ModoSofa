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

public static class Versiones
{
    /// <summary>Repositorio donde se publican los releases.</summary>
    public const string Repositorio = "CapicuaSoft/ModoSofa";

    /// <summary>
    /// "v1.2.3", "1.2", "1.2.3-beta" o "1.2.3+abc" → Version. Null si no es una versión.
    /// Se compara como números: como texto, "1.10.0" sería menor que "1.9.0".
    /// </summary>
    public static Version? Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(limpio, out var v)) return null;
        // Version trata los componentes ausentes como -1: se normaliza a 0 para que 1.2 == 1.2.0.
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    public static bool EsMasNueva(string? publicada, string? actual)
    {
        var p = Leer(publicada);
        var a = Leer(actual);
        return p is not null && a is not null && p > a;
    }
}

public static class SumasSha256
{
    /// <summary>
    /// Busca en un SHA256SUMS.txt («hash  fichero» por línea, LF o CRLF, con o sin «*» de modo
    /// binario) el hash de un fichero. Null si no aparece.
    /// </summary>
    public static string? Buscar(string sumas, string fichero)
    {
        foreach (var linea in sumas.Split('\n'))
        {
            var partes = linea.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2 || partes[0].Length != 64) continue;
            if (partes[1].Trim().TrimStart('*').Equals(fichero, StringComparison.OrdinalIgnoreCase))
                return partes[0].ToLowerInvariant();
        }
        return null;
    }
}
