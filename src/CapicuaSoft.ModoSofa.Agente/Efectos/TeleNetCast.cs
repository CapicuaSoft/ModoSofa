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

using System.Net;
using System.Text;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Efectos;

/// <summary>
/// Teles LG NetCast (UDAP por HTTP, puerto 8080). Probado con la 50LB5830 (NetCast 4.0).
/// Apagada no escucha la red: no se puede encender. Para hablar con ella hay que saludarla con el
/// código de vinculación; se saluda antes de cada orden porque la sesión caduca.
/// </summary>
public sealed class TeleNetCast(Configuracion config, AlmacenSecretos secretos, ILogger<TeleNetCast> log)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private string Base => $"http://{config.Tele.LgIp}:8080/udap/api/";

    public bool Vinculada => !string.IsNullOrEmpty(secretos.Valor.LgCodigoNetCast);

    /// <summary>Modelo si la tele responde como NetCast; null si no (apagada, webOS u otra cosa).</summary>
    public async Task<string?> ModeloAsync(CancellationToken ct = default)
    {
        try
        {
            using var r = await Enviar(HttpMethod.Get, "data?target=netrcu.model", null, ct);
            return r.IsSuccessStatusCode ? NetCastProtocolo.Modelo(await r.Content.ReadAsStringAsync(ct)) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>La tele muestra en pantalla el código de vinculación.</summary>
    public async Task MostrarCodigoAsync(CancellationToken ct = default)
    {
        using var r = await Enviar(HttpMethod.Post, "pairing", NetCastProtocolo.PedirCodigo(), ct);
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"La tele no mostró el código (HTTP {(int)r.StatusCode}).");
        log.LogInformation("Tele: mostrando el código de vinculación en pantalla.");
    }

    /// <summary>Comprueba el código con la tele y, si lo acepta, lo guarda.</summary>
    public async Task VincularAsync(string codigo, CancellationToken ct = default)
    {
        using var r = await Enviar(HttpMethod.Post, "pairing", NetCastProtocolo.Saludo(codigo), ct);
        if (r.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("La tele no acepta ese código.");
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"La tele respondió HTTP {(int)r.StatusCode}.");
        secretos.Modificar(s => s.LgCodigoNetCast = new string(codigo.Where(char.IsAsciiDigit).ToArray()));
        log.LogInformation("Tele vinculada.");
    }

    public Task ApagarAsync(CancellationToken ct = default) => PulsarAsync(NetCastProtocolo.Tecla.Encendido, ct);

    public async Task PulsarAsync(NetCastProtocolo.Tecla tecla, CancellationToken ct = default)
    {
        if (!Vinculada) throw new InvalidOperationException("La tele no está vinculada: en el móvil, Ajustes → Tele → Vincular.");
        using (var saludo = await Enviar(HttpMethod.Post, "pairing", NetCastProtocolo.Saludo(secretos.Valor.LgCodigoNetCast), ct))
        {
            if (saludo.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("La tele ya no reconoce el código: vuelve a vincularla.");
        }
        using var r = await Enviar(HttpMethod.Post, "command", NetCastProtocolo.Pulsar(tecla), ct);
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"La tele rechazó la orden (HTTP {(int)r.StatusCode}).");
        log.LogInformation("Tele: {Tecla}.", tecla);
    }

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string? xml, CancellationToken ct)
    {
        var peticion = new HttpRequestMessage(metodo, Base + ruta);
        peticion.Headers.UserAgent.ParseAdd(NetCastProtocolo.AgenteDeUsuario);
        if (xml is not null) peticion.Content = new StringContent(xml, Encoding.UTF8, "text/xml");
        return Http.SendAsync(peticion, ct);
    }
}

/// <summary>
/// La tele LG, sea del tipo que sea: webOS (encender, cambiar de entrada, apagar) o NetCast (solo
/// apagar). Con <c>Tele.LgTipo = "Auto"</c> se averigua preguntándole a la tele.
/// </summary>
public sealed class TeleLg(Configuracion config, TeleWebOs webOs, TeleNetCast netCast, ILogger<TeleLg> log)
{
    private bool? _esNetCast;

    public bool Configurada => config.Tele.ControlarLg && !string.IsNullOrWhiteSpace(config.Tele.LgIp);

    public TeleNetCast NetCast => netCast;

    public async Task<bool> EsNetCastAsync(CancellationToken ct = default)
    {
        if (config.Tele.LgTipo.Equals("NetCast", StringComparison.OrdinalIgnoreCase)) return true;
        if (config.Tele.LgTipo.Equals("WebOs", StringComparison.OrdinalIgnoreCase)) return false;
        if (_esNetCast is { } sabido) return sabido;
        var modelo = await netCast.ModeloAsync(ct);
        if (modelo is null) return false; // no se recuerda: quizá estaba apagada
        log.LogInformation("Tele LG NetCast detectada: {Modelo}.", modelo);
        return (_esNetCast = true).Value;
    }

    public async Task EncenderAsync(CancellationToken ct = default)
    {
        if (await EsNetCastAsync(ct))
            throw new InvalidOperationException("Esta tele (NetCast) no se puede encender por red: enciéndela con su mando.");
        await webOs.EncenderAsync(ct);
    }

    public async Task ApagarAsync(CancellationToken ct = default)
    {
        if (await EsNetCastAsync(ct)) await netCast.ApagarAsync(ct);
        else await webOs.ApagarAsync(ct);
    }

    /// <summary>Al entrar en modo sofá: en webOS, encender y cambiar de entrada; en NetCast, nada.</summary>
    public async Task PrepararAsync(CancellationToken ct = default)
    {
        if (await EsNetCastAsync(ct)) return;
        await webOs.PrepararAsync(ct);
    }
}
