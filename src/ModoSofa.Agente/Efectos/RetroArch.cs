using System.Net.Sockets;
using System.Text;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Efectos;

/// <summary>
/// Comandos de red de RetroArch (network_cmd_enable = "true", UDP 55355). Permiten salir y pausar
/// limpiamente, de modo que se guarden la SRAM y el estado automático.
/// </summary>
public sealed class RetroArch(Configuracion config, ILogger<RetroArch> log)
{
    public const string NombreProceso = "retroarch";

    public bool Abierto() => System.Diagnostics.Process.GetProcessesByName(NombreProceso).Length > 0;

    public async Task EnviarAsync(string comando)
    {
        using var udp = new UdpClient();
        var datos = Encoding.ASCII.GetBytes(comando);
        await udp.SendAsync(datos, datos.Length, "127.0.0.1", config.Lanzadores.RetroArchPuerto);
    }

    /// <summary>"PLAYING", "PAUSED", "CONTENTLESS" o null si RetroArch no contesta (comandos desactivados).</summary>
    public async Task<string?> EstadoAsync()
    {
        using var udp = new UdpClient();
        udp.Connect("127.0.0.1", config.Lanzadores.RetroArchPuerto);
        var datos = Encoding.ASCII.GetBytes("GET_STATUS");
        await udp.SendAsync(datos, datos.Length);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
        try
        {
            var r = await udp.ReceiveAsync(cts.Token);
            return RetroArchProtocolo.EstadoDe(Encoding.ASCII.GetString(r.Buffer));
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException)
        {
            return null;
        }
    }

    /// <summary>Pide salir. Con quit_press_twice la primera orden solo avisa: se envía dos veces.</summary>
    public async Task<bool> SalirAsync(TimeSpan espera)
    {
        if (!Abierto()) return true;
        if (await EstadoAsync() is null)
        {
            log.LogInformation("RetroArch no responde a comandos de red (¿network_cmd_enable desactivado?).");
            return false;
        }
        await EnviarAsync("QUIT");
        await Task.Delay(400);
        if (Abierto()) await EnviarAsync("QUIT");

        var limite = DateTime.UtcNow + espera;
        while (DateTime.UtcNow < limite)
        {
            if (!Abierto()) return true;
            await Task.Delay(200);
        }
        return !Abierto();
    }
}
