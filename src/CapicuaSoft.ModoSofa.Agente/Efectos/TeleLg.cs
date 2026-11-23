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

using System.Net.Security;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Efectos;

/// <summary>
/// Tele LG webOS por su API local SSAP (WebSocket, 3001 con TLS autofirmado o 3000 en claro).
/// La primera vez la tele muestra un aviso que hay que aceptar con su mando; la clave que devuelve
/// se guarda en secretos.json y las siguientes veces no pregunta.
/// </summary>
public sealed class TeleLg(Configuracion config, AlmacenSecretos secretos, ILogger<TeleLg> log)
{
    private static readonly string[] Permisos =
    [
        "LAUNCH", "CONTROL_AUDIO", "CONTROL_POWER", "CONTROL_INPUT_TV", "CONTROL_INPUT_MEDIA_PLAYBACK",
        "READ_INPUT_DEVICE_LIST", "READ_POWER_STATE", "READ_INSTALLED_APPS", "READ_TV_CURRENT_TIME",
    ];

    public bool Configurada => config.Tele.ControlarLg && !string.IsNullOrWhiteSpace(config.Tele.LgIp);

    public async Task EncenderAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Tele.LgMac))
            throw new InvalidOperationException("Falta Tele.LgMac para encender la tele.");
        await WakeOnLan.EnviarAsync(config.Tele.LgMac);
        log.LogInformation("Tele: paquete de encendido enviado.");
    }

    public Task ApagarAsync(CancellationToken ct = default) => PedirAsync("ssap://system/turnOff", null, ct);

    public Task CambiarEntradaAsync(string entrada, CancellationToken ct = default) =>
        PedirAsync("ssap://tv/switchInput", new JsonObject { ["inputId"] = entrada }, ct);

    /// <summary>Enciende la tele, espera a que responda y cambia a la entrada configurada.</summary>
    public async Task PrepararAsync(CancellationToken ct = default)
    {
        if (config.Tele.EncenderAlEntrar && !string.IsNullOrWhiteSpace(config.Tele.LgMac))
            await EncenderAsync(ct);
        if (string.IsNullOrWhiteSpace(config.Tele.LgEntrada)) return;

        for (var intento = 0; intento < 10; intento++)
        {
            try
            {
                await CambiarEntradaAsync(config.Tele.LgEntrada, ct);
                log.LogInformation("Tele en {Entrada}.", config.Tele.LgEntrada);
                return;
            }
            catch (Exception) when (intento < 9)
            {
                await Task.Delay(2000, ct); // la tele tarda en levantar la red tras encenderse
            }
        }
    }

    private async Task PedirAsync(string uri, JsonObject? datos, CancellationToken ct)
    {
        if (!Configurada) throw new InvalidOperationException("La tele LG no está configurada (Tele.ControlarLg y Tele.LgIp).");

        using var ws = await ConectarAsync(ct);
        await RegistrarAsync(ws, ct);

        var peticion = new JsonObject { ["type"] = "request", ["id"] = "orden", ["uri"] = uri };
        if (datos is not null) peticion["payload"] = datos;
        await EnviarAsync(ws, peticion, ct);

        var respuesta = await RecibirHastaAsync(ws, m => (string?)m["id"] == "orden", ct);
        if ((string?)respuesta["type"] == "error")
            throw new InvalidOperationException($"La tele rechazó {uri}: {respuesta["error"]}");
        log.LogInformation("Tele: {Uri} correcto.", uri);
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { /* la tele puede cortar al apagarse */ }
    }

    private async Task<ClientWebSocket> ConectarAsync(CancellationToken ct)
    {
        foreach (var url in new[] { $"wss://{config.Tele.LgIp}:3001/", $"ws://{config.Tele.LgIp}:3000/" })
        {
            var ws = new ClientWebSocket();
            // La tele usa un certificado autofirmado y solo se le habla dentro de la red de casa.
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                await ws.ConnectAsync(new Uri(url), limite.Token);
                return ws;
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or AuthenticationException)
            {
                ws.Dispose();
            }
        }
        throw new InvalidOperationException($"No se pudo conectar con la tele en {config.Tele.LgIp}.");
    }

    private async Task RegistrarAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var s = secretos.Valor;
        var payload = new JsonObject
        {
            ["forcePairing"] = false,
            ["pairingType"] = "PROMPT",
            ["manifest"] = new JsonObject
            {
                ["manifestVersion"] = 1,
                ["appVersion"] = "1.0",
                ["permissions"] = new JsonArray(Permisos.Select(p => (JsonNode)p).ToArray()),
            },
        };
        if (!string.IsNullOrEmpty(s.LgClaveCliente)) payload["client-key"] = s.LgClaveCliente;

        await EnviarAsync(ws, new JsonObject { ["type"] = "register", ["id"] = "registro", ["payload"] = payload }, ct);

        if (string.IsNullOrEmpty(s.LgClaveCliente))
            log.LogWarning("Tele: acepta el aviso que aparece en la pantalla de la tele (solo la primera vez).");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(60));
        var registrado = await RecibirHastaAsync(ws, m => (string?)m["type"] is "registered" or "error", limite.Token);
        if ((string?)registrado["type"] == "error")
            throw new InvalidOperationException($"La tele rechazó el emparejamiento: {registrado["error"]}");

        var clave = (string?)registrado["payload"]?["client-key"];
        if (!string.IsNullOrEmpty(clave) && clave != s.LgClaveCliente)
        {
            secretos.Modificar(x => x.LgClaveCliente = clave);
            log.LogInformation("Tele emparejada; clave guardada.");
        }
    }

    private static async Task EnviarAsync(ClientWebSocket ws, JsonObject mensaje, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(mensaje.ToJsonString());
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonNode> RecibirHastaAsync(ClientWebSocket ws, Func<JsonNode, bool> condicion, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (true)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(buffer, ct);
                if (r.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("La tele cerró la conexión.");
                ms.Write(buffer, 0, r.Count);
            } while (!r.EndOfMessage);

            var nodo = JsonNode.Parse(ms.ToArray());
            if (nodo is not null && condicion(nodo)) return nodo;
        }
    }
}
