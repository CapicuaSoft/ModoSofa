namespace ModoSofa.Nucleo;

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
