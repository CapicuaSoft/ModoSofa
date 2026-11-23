// ModoSofá - Convierte el PC de trabajo en una consola de salón.
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
    VersionNueva? VersionNueva,
    string Actualizacion);

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
        try { activo = lanzadores.Delante(overlay.VentanaDelante); } catch { activo = new Activo(TipoActivo.Ninguno, "", 0, IntPtr.Zero); }

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
            actualizacion.Disponible,
            actualizacion.Situacion);
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
