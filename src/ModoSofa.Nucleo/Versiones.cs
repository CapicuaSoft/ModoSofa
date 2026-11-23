namespace ModoSofa.Nucleo;

public static class Versiones
{
    /// <summary>Repositorio donde se publican los releases.</summary>
    public const string Repositorio = "jbautistamartin/ModoSofa";

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
