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

using System.Security.Cryptography;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Infraestructura;

public static class Rutas
{
    private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string Locales = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static readonly string Datos = Path.Combine(Roaming, "CapicuaSoft", "ModoSofa");
    public static readonly string Local = Path.Combine(Locales, "CapicuaSoft", "ModoSofa");
    public static readonly string Config = Path.Combine(Datos, "config.json");
    public static readonly string Estado = Path.Combine(Datos, "estado.json");
    public static readonly string Secretos = Path.Combine(Datos, "secretos.json");
    public static readonly string Logs = Path.Combine(Local, "logs");
    public static readonly string WebView2 = Path.Combine(Local, "WebView2");

    /// <summary>
    /// Hasta la 0.2.0 los datos vivían en %APPDATA%\ModoSofa y %LOCALAPPDATA%\ModoSofa. Si existen y
    /// las carpetas nuevas no, se mueven: se conservan configuración, tokens (vinculación del móvil y
    /// de Alexa), estado del modo sofá y logs. Se llama antes de cargar nada.
    /// </summary>
    public static List<string> Migrar()
    {
        var movidas = new List<string>();
        foreach (var (antigua, nueva) in new[] { (Path.Combine(Roaming, "ModoSofa"), Datos), (Path.Combine(Locales, "ModoSofa"), Local) })
        {
            if (!Directory.Exists(antigua) || Directory.Exists(nueva)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(nueva)!);
            Directory.Move(antigua, nueva);
            movidas.Add($"{antigua} → {nueva}");
        }
        return movidas;
    }
}

/// <summary>Estado persistente (lo necesario para deshacer el modo sofá).</summary>
public sealed class AlmacenEstado
{
    private readonly Lock _cerrojo = new();
    public EstadoPersistente Valor { get; } = Ficheros.CargarJson<EstadoPersistente>(Rutas.Estado);

    public void Guardar()
    {
        lock (_cerrojo) Ficheros.GuardarJson(Rutas.Estado, Valor);
    }
}

/// <summary>Tokens del móvil y de Alexa, y la clave de la tele. Se generan en el primer arranque.</summary>
public sealed class AlmacenSecretos
{
    private readonly Lock _cerrojo = new();
    public Secretos Valor { get; }

    public AlmacenSecretos()
    {
        Valor = Ficheros.CargarJson<Secretos>(Rutas.Secretos);
        var cambios = false;
        if (string.IsNullOrEmpty(Valor.TokenApi)) { Valor.TokenApi = NuevoToken(); cambios = true; }
        if (string.IsNullOrEmpty(Valor.TokenAlexa)) { Valor.TokenAlexa = NuevoToken(); cambios = true; }
        if (cambios) Ficheros.GuardarJson(Rutas.Secretos, Valor);
    }

    public void Modificar(Action<Secretos> cambio)
    {
        lock (_cerrojo)
        {
            cambio(Valor);
            Ficheros.GuardarJson(Rutas.Secretos, Valor);
        }
    }

    public void RegenerarTokenApi() => Modificar(s => s.TokenApi = NuevoToken());

    public static bool Iguales(string? a, string b) =>
        a is not null && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));

    private static string NuevoToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
