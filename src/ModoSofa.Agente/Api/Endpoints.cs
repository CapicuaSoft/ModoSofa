using System.Diagnostics;
using ModoSofa.Agente.Efectos;
using ModoSofa.Agente.Infraestructura;
using ModoSofa.Agente.Servicios;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Api;

public static class Endpoints
{
    /// <summary>
    /// /api/* exige el token del móvil (cabecera Authorization: Bearer o ?t= para EventSource).
    /// /api/alexa exige su propio token: es lo único que se expone a internet (Tailscale Funnel).
    /// </summary>
    public static void UsarSeguridad(this WebApplication app)
    {
        app.Use(async (ctx, siguiente) =>
        {
            var ruta = ctx.Request.Path;
            if (!ruta.StartsWithSegments("/api"))
            {
                await siguiente();
                return;
            }

            var secretos = ctx.RequestServices.GetRequiredService<AlmacenSecretos>().Valor;
            var cabecera = ctx.Request.Headers.Authorization.ToString();
            var token = cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? cabecera["Bearer ".Length..].Trim()
                : ctx.Request.Query["t"].ToString();

            var esperado = ruta.StartsWithSegments("/api/alexa") ? secretos.TokenAlexa : secretos.TokenApi;
            if (!AlmacenSecretos.Iguales(token, esperado))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            await siguiente();
        });
    }

    public static void MapearApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/estado", (PanelEstado panel) => panel.Tomar());

        api.MapGet("/acciones", (RegistroAcciones r) =>
            r.Todas.Select(a => new { a.Id, a.Nombre, a.Grupo, a.Peligrosa }));

        api.MapPost("/acciones/{id}", (string id, RegistroAcciones r) =>
            r.Lanzar(id, "móvil") ? Results.Accepted() : Results.NotFound());

        api.MapGet("/eventos", async (HttpContext ctx, Eventos eventos, PanelEstado panel, RegistroLog registroLog) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            var (lector, baja) = eventos.Suscribir();
            using (baja)
            {
                eventos.Publicar("estado", panel.Tomar());
                var ct = ctx.RequestAborted;
                while (!ct.IsCancellationRequested)
                {
                    using var latido = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    latido.CancelAfter(TimeSpan.FromSeconds(15));
                    try
                    {
                        var mensaje = await lector.ReadAsync(latido.Token);
                        await ctx.Response.WriteAsync(mensaje, ct);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        await ctx.Response.WriteAsync(": latido\n\n", ct); // mantiene viva la conexión
                    }
                    catch (OperationCanceledException) { break; }
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
        });

        api.MapGet("/log", (RegistroLog l, int? n) => l.Ultimas(Math.Clamp(n ?? 300, 1, 1000)));
        api.MapDelete("/log", (RegistroLog l) => { l.Vaciar(); return Results.NoContent(); });

        api.MapGet("/config", () => Results.Text(File.ReadAllText(Rutas.Config), "application/json"));

        api.MapPut("/config", async (HttpRequest req, IHostApplicationLifetime vida, ILoggerFactory lf) =>
        {
            var json = await new StreamReader(req.Body).ReadToEndAsync();
            try
            {
                Configuracion.Deserializar(json).Guardar(Rutas.Config);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            lf.CreateLogger("ModoSofa.Api").LogInformation("Configuración guardada desde el móvil; reiniciando.");
            Reiniciar(vida);
            return Results.Ok(new { reiniciando = true });
        });

        api.MapGet("/diagnostico", async (Pantallas pantallas, Audio audio, Lanzadores lanzadores, RetroArch ra,
            Configuracion config, MandoServicio mando) =>
        {
            object? Probar(Func<object?> f)
            {
                try { return f(); }
                catch (Exception ex) { return new { error = ex.Message }; }
            }

            return new
            {
                version = PanelEstado.Version,
                pantallas = Probar(() => pantallas.Conectadas()),
                tele = new { fragmento = config.Tele.NombrePantalla, conectada = Probar(() => pantallas.EstaConectada(config.Tele.NombrePantalla)) },
                audio = Probar(() => audio.Salidas().Select(d => new { d.Nombre, d.PorDefecto })),
                audioSofa = config.Audio.DispositivoSofa,
                steam = Probar(() => lanzadores.RutaSteam()),
                esde = new { ruta = config.Lanzadores.EsdeExe, existe = File.Exists(config.Lanzadores.EsdeExe) },
                retroArch = new { abierto = ra.Abierto(), comandosDeRed = ra.Abierto() ? await ra.EstadoAsync() ?? "sin respuesta" : "(cerrado)" },
                activo = Probar(() => { var a = lanzadores.Detectar(); return new { tipo = a.Tipo.ToString(), a.Nombre }; }),
                mandos = mando.Conectados,
                red = Probar(() => Red()),
                rutas = new { config = Rutas.Config, logs = Rutas.Logs },
            };
        });

        api.MapPost("/alexa", (PeticionAlexa peticion, Configuracion config, RegistroAcciones r, ILoggerFactory lf) =>
        {
            if (!config.Alexa.Activada) return Results.NotFound();
            var (accion, respuesta) = PuenteAlexa.Resolver(peticion);
            lf.CreateLogger("ModoSofa.Alexa").LogInformation("Alexa: {Intent} → {Accion}", peticion.Intent, accion ?? "(sin acción)");
            if (accion is not null && !r.Lanzar(accion, "Alexa"))
                return Results.Ok(new RespuestaAlexa("Esa acción no existe en el equipo.", true, null));
            return Results.Ok(respuesta);
        });
    }

    /// <summary>Interfaces activas con su IPv4 y MAC: la app Android guarda la MAC para el Wake-on-LAN.</summary>
    private static object Red() =>
        System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                     && n.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                        or System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211
                     // Los adaptadores virtuales (Hyper-V, VPN…) no despiertan el equipo.
                     && !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                     && !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase))
            .Select(n => new
            {
                nombre = n.Name,
                cable = n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                mac = string.Join(":", n.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                ip = n.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString(),
            })
            .Where(n => n.ip is not null && n.mac.Length > 0)
            .ToList();

    /// <summary>Lanza una nueva instancia que espera a que esta suelte el mutex, y se cierra.</summary>
    private static void Reiniciar(IHostApplicationLifetime vida)
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--esperar") { UseShellExecute = false });
        _ = Task.Delay(300).ContinueWith(_ => vida.StopApplication());
    }
}
