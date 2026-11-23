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

using System.Text.Json;

namespace ModoSofa.Nucleo;

public static class Ficheros
{
    /// <summary>
    /// Escribe a un temporal y lo mueve encima: un corte a mitad nunca deja el fichero a medias,
    /// que es justo lo que el estado persistente no se puede permitir.
    /// </summary>
    public static void EscribirAtomico(string ruta, string contenido)
    {
        var carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);
        var temporal = ruta + ".tmp";
        File.WriteAllText(temporal, contenido);
        File.Move(temporal, ruta, overwrite: true);
    }

    public static T CargarJson<T>(string ruta) where T : new()
    {
        if (!File.Exists(ruta)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(ruta), Configuracion.OpcionesJson) ?? new T();
        }
        catch (JsonException)
        {
            // Un fichero corrupto no debe impedir arrancar; se aparta para poder revisarlo.
            File.Move(ruta, ruta + ".corrupto", overwrite: true);
            return new T();
        }
    }

    public static void GuardarJson<T>(string ruta, T valor) =>
        EscribirAtomico(ruta, JsonSerializer.Serialize(valor, Configuracion.OpcionesJson));
}
