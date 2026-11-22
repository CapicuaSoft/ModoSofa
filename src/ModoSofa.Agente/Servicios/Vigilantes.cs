using ModoSofa.Agente.Efectos;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Servicios;

/// <summary>Detecta la tele por su nombre EDID y entra o sale del modo sofá solo.</summary>
public sealed class VigilantePantallas(
    Configuracion config, Pantallas pantallas, ModoServicio modo, RegistroAcciones registro,
    ILogger<VigilantePantallas> log) : BackgroundService
{
    public bool TeleConectada { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var detector = new DetectorTele(config.Tele.EsperaDeteccionMs);
        string? ultimoError = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var conectada = pantallas.EstaConectada(config.Tele.NombrePantalla);
                TeleConectada = conectada;
                switch (detector.Procesar(conectada, Environment.TickCount64))
                {
                    case true:
                        log.LogInformation("Tele conectada.");
                        if (config.Tele.EntrarAlConectar && modo.Modo == Modo.Trabajo)
                            registro.Lanzar("modo.sofa", "tele");
                        break;
                    case false:
                        log.LogInformation("Tele desconectada.");
                        if (config.Tele.SalirAlDesconectar && modo.Modo == Modo.Sofa)
                            registro.Lanzar("modo.trabajo", "tele");
                        break;
                }
                ultimoError = null;
            }
            catch (Exception ex) when (ex.Message != ultimoError)
            {
                ultimoError = ex.Message;
                log.LogWarning(ex, "No se pudo consultar las pantallas.");
            }
            catch { /* mismo error que la vez anterior: no repetir en el log */ }

            await Task.Delay(1000, ct);
        }
    }
}

/// <summary>
/// En modo sofá, si una ventana de un programa de trabajo (los que tenían ventanas al entrar) se
/// pone delante, se minimiza y se devuelve el foco al juego. Los procesos nuevos —juegos y
/// lanzadores— nunca se tocan, así que un juego desconocido no puede ser minimizado por error.
/// </summary>
public sealed class GuardianFoco(
    Configuracion config, ModoServicio modo, Ventanas ventanas, Lanzadores lanzadores, IOverlay overlay,
    ILogger<GuardianFoco> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.Guardian.Activado) return;
        var avisadas = new HashSet<IntPtr>();

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(config.Guardian.IntervaloMs, ct);
            if (modo.Modo != Modo.Sofa || !modo.GuardianActivo || overlay.Visible)
            {
                avisadas.Clear();
                continue;
            }

            try
            {
                var delante = Nativo.GetForegroundWindow();
                if (delante == IntPtr.Zero) continue;
                var raiz = Nativo.GetAncestor(delante, Nativo.GA_ROOTOWNER);
                if (raiz == IntPtr.Zero) raiz = delante;
                var pid = Nativo.ProcesoDeVentana(raiz);
                if (pid == Environment.ProcessId || !modo.ProcesosTrabajo.Contains(pid)) continue;
                if (!Ventanas.EsDeUsuario(raiz)) continue;

                if (avisadas.Add(raiz))
                    log.LogInformation("Guardián: «{Titulo}» ({Proceso}) se puso delante; se aparta.",
                        Nativo.TextoVentana(raiz), Ventanas.NombreProceso(pid));
                ventanas.Minimizar(raiz);
                await Task.Delay(150, ct);
                var activo = lanzadores.Detectar();
                if (activo.Hwnd != IntPtr.Zero) ventanas.Enfocar(activo.Hwnd);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Guardián de foco.");
            }
        }
    }
}

/// <summary>Lee los mandos (XInput) en un hilo propio y convierte combinaciones en acciones.</summary>
public sealed class MandoServicio(
    Configuracion config, RegistroAcciones registro, IOverlay overlay, ILogger<MandoServicio> log) : BackgroundService
{
    public int Conectados { get; private set; }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.Mando.Activado) return Task.CompletedTask;

        DetectorCombos[] detectores;
        try
        {
            detectores = Enumerable.Range(0, XInput.MaxMandos).Select(_ => DetectorCombos.DesdeConfig(config.Mando)).ToArray();
        }
        catch (FormatException ex)
        {
            log.LogError("Combinaciones del mando no válidas: {Mensaje}", ex.Message);
            return Task.CompletedTask;
        }

        var hilo = new Thread(() => Bucle(detectores, ct)) { IsBackground = true, Name = "ModoSofa.Mando" };
        hilo.Start();
        log.LogInformation("Mando: {Overlay} mantenido abre el menú; {Atajos} atajos.", config.Mando.ComboOverlay, config.Mando.Atajos.Count);
        return Task.CompletedTask;
    }

    private void Bucle(DetectorCombos[] detectores, CancellationToken ct)
    {
        var conectado = new bool[XInput.MaxMandos];
        var proximaComprobacion = new long[XInput.MaxMandos];

        while (!ct.IsCancellationRequested)
        {
            var ahora = Environment.TickCount64;
            for (var i = 0; i < XInput.MaxMandos; i++)
            {
                // Consultar una ranura vacía es caro en XInput: se mira solo una vez por segundo.
                if (!conectado[i] && ahora < proximaComprobacion[i]) continue;
                var botones = XInput.Leer(i);
                if (botones is null)
                {
                    if (conectado[i]) log.LogInformation("Mando {Indice} desconectado.", i + 1);
                    conectado[i] = false;
                    proximaComprobacion[i] = ahora + 1000;
                    continue;
                }
                if (!conectado[i]) log.LogInformation("Mando {Indice} conectado.", i + 1);
                conectado[i] = true;

                foreach (var evento in detectores[i].Procesar(botones.Value, ahora))
                {
                    try
                    {
                        switch (evento)
                        {
                            case AlternarOverlay:
                                overlay.Alternar();
                                break;
                            case Atajo a:
                                registro.Lanzar(a.AccionId, "mando");
                                break;
                            case Pulsado p when overlay.Visible:
                                overlay.Boton(p.Boton);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Error atendiendo al mando.");
                    }
                }
            }
            Conectados = conectado.Count(c => c);
            Thread.Sleep(16);
        }
    }
}
