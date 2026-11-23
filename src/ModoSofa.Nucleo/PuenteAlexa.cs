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

namespace ModoSofa.Nucleo;

/// <summary>Lo que la Lambda envía: el intent y, si lo había, la acción pendiente de confirmar.</summary>
public sealed record PeticionAlexa(string Intent, string? Pendiente);

/// <summary>Lo que la Lambda debe decir. <see cref="Pendiente"/> se guarda en la sesión de Alexa.</summary>
public sealed record RespuestaAlexa(string Habla, bool Terminar, string? Pendiente);

/// <summary>
/// Traducción de intents a acciones. Vive en el PC, no en la Lambda: añadir una orden solo exige
/// tocar esta tabla y el modelo de interacción.
/// </summary>
public static class PuenteAlexa
{
    private sealed record Entrada(string Accion, string Habla, string? Confirmacion = null);

    private static readonly Dictionary<string, Entrada> Intents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ActivarModoSofaIntent"] = new("modo.sofa", "Modo sofá en marcha."),
        ["VolverATrabajarIntent"] = new("modo.trabajo", "Volviendo al modo trabajo."),
        ["CerrarJuegoIntent"] = new("juego.cerrar", "Cerrando el juego."),
        ["EnfocarJuegoIntent"] = new("juego.enfocar", "Ahí tienes el juego."),
        ["SubirVolumenIntent"] = new("volumen.subir", "Volumen subido."),
        ["BajarVolumenIntent"] = new("volumen.bajar", "Volumen bajado."),
        ["SilenciarIntent"] = new("volumen.silenciar", "Hecho."),
        ["AbrirSteamIntent"] = new("steam.bigpicture", "Abriendo Steam."),
        ["CerrarSteamIntent"] = new("steam.cerrar", "Cerrando Steam."),
        ["AbrirEmulationStationIntent"] = new("esde.abrir", "Abriendo EmulationStation."),
        ["ApagarTeleIntent"] = new("tele.apagar", "Apagando la tele."),
        ["SuspenderEquipoIntent"] = new("equipo.suspender", "Suspendiendo el equipo.",
            "¿Seguro que quieres suspender el equipo?"),
        ["ReiniciarEquipoIntent"] = new("equipo.reiniciar", "Reiniciando el equipo.",
            "¿Seguro que quieres reiniciar el equipo?"),
        ["ApagarEquipoIntent"] = new("equipo.apagar", "Apagando el equipo. Hasta pronto.",
            "¿Seguro que quieres apagar el equipo?"),
    };

    public const string Ayuda =
        "Puedo activar el modo sofá o volver al trabajo, cerrar o enfocar el juego, " +
        "ajustar el volumen, abrir Steam o EmulationStation, y apagar la tele o el equipo. ¿Qué quieres hacer?";

    public static IEnumerable<string> IntentsConocidos => Intents.Keys;

    /// <summary>Decide qué acción ejecutar (si alguna) y qué responder.</summary>
    public static (string? Accion, RespuestaAlexa Respuesta) Resolver(PeticionAlexa peticion)
    {
        switch (peticion.Intent)
        {
            case "LaunchRequest":
                return (null, new RespuestaAlexa("Modo sofá listo. ¿Qué hago?", false, null));
            case "AMAZON.HelpIntent":
                return (null, new RespuestaAlexa(Ayuda, false, peticion.Pendiente));
            case "AMAZON.StopIntent" or "AMAZON.CancelIntent":
                return (null, new RespuestaAlexa("Hasta luego.", true, null));
            case "AMAZON.NoIntent":
                return (null, new RespuestaAlexa("Vale, no hago nada.", true, null));
            case "AMAZON.YesIntent":
                var pendiente = peticion.Pendiente is null ? null
                    : Intents.Values.FirstOrDefault(e => e.Accion == peticion.Pendiente);
                return pendiente is null
                    ? (null, new RespuestaAlexa("No había nada pendiente.", true, null))
                    : (pendiente.Accion, new RespuestaAlexa(pendiente.Habla, true, null));
        }

        if (!Intents.TryGetValue(peticion.Intent, out var entrada))
            return (null, new RespuestaAlexa("No sé hacer eso todavía.", true, null));

        if (entrada.Confirmacion is not null)
            return (null, new RespuestaAlexa(entrada.Confirmacion, false, entrada.Accion));

        return (entrada.Accion, new RespuestaAlexa(entrada.Habla, true, null));
    }
}
