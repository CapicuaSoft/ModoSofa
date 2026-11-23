using System.Reflection;
using System.Text.Json;
using ModoSofa.Agente.Efectos;
using ModoSofa.Agente.Infraestructura;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Servicios;

public sealed record Instantanea(
    string Modo,
    DateTimeOffset? Desde,
    bool TeleConectada,
    string? Audio,
    string Activo,
    string TipoActivo,
    bool Guardian,
    bool Overlay,
    int Mandos,
    string? Ocupado,
    string Version,
    VersionNueva? VersionNueva);

/// <summary>Estado en vivo para el móvil y el overlay; se publica por SSE cuando cambia.</summary>
public sealed class PanelEstado(
    ModoServicio modo, VigilantePantallas vigilante, Audio audio, Lanzadores lanzadores, MandoServicio mando,
    IOverlay overlay, RegistroAcciones registro, Eventos eventos, ActualizacionServicio actualizacion,
    ILogger<PanelEstado> log) : BackgroundService
{
    public static readonly string Version =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    private volatile string? _ocupado;
    private readonly SemaphoreSlim _despertar = new(0);

    public Instantanea Tomar()
    {
        string? nombreAudio = null;
        try { nombreAudio = audio.NombreActual(); } catch { /* sin audio */ }
        Activo activo;
        try { activo = lanzadores.Detectar(); } catch { activo = new Activo(TipoActivo.Ninguno, "", 0, IntPtr.Zero); }

        return new Instantanea(
            modo.Modo == Modo.Sofa ? "sofa" : "trabajo",
            modo.Desde,
            vigilante.TeleConectada,
            nombreAudio,
            activo.Nombre,
            activo.Tipo.ToString(),
            modo.GuardianActivo,
            overlay.Visible,
            mando.Conectados,
            _ocupado,
            Version,
            actualizacion.Disponible);
    }

    public void Avisar() => _despertar.Release();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        registro.Iniciada += (id, origen) =>
        {
            _ocupado = registro.Buscar(id)?.Nombre;
            log.LogInformation("▶ {Accion} (desde {Origen})", registro.Buscar(id)?.Nombre ?? id, origen);
            eventos.Publicar("accion", new { id, origen, fase = "inicio" });
            Avisar();
        };
        registro.Terminada += (r, origen) =>
        {
            _ocupado = null;
            if (!r.Correcta) log.LogError("✖ {Accion}: {Error}", r.Id, r.Error);
            eventos.Publicar("accion", new { id = r.Id, origen, fase = "fin", correcta = r.Correcta, error = r.Error });
            Avisar();
        };
        modo.Cambio += Avisar;
        actualizacion.Cambio += Avisar;

        string? anterior = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var actual = Tomar();
                var json = JsonSerializer.Serialize(actual);
                if (json != anterior)
                {
                    anterior = json;
                    eventos.Publicar("estado", actual);
                }
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Instantánea de estado.");
            }
            await _despertar.WaitAsync(TimeSpan.FromSeconds(2), ct);
        }
    }
}
