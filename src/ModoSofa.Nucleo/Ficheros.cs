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
