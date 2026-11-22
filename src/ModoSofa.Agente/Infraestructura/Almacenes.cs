using System.Security.Cryptography;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Infraestructura;

public static class Rutas
{
    public static readonly string Datos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModoSofa");
    public static readonly string Local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModoSofa");
    public static readonly string Config = Path.Combine(Datos, "config.json");
    public static readonly string Estado = Path.Combine(Datos, "estado.json");
    public static readonly string Secretos = Path.Combine(Datos, "secretos.json");
    public static readonly string Logs = Path.Combine(Local, "logs");
    public static readonly string WebView2 = Path.Combine(Local, "WebView2");
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
