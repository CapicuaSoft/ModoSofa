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

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Servicios;

public sealed record VersionNueva(string Version, string Url);

/// <summary>
/// Actualización automática. Pregunta a GitHub por el último release como mucho una vez al día
/// (con ETag: un 304 no gasta cuota) y, si hay uno más nuevo:
///  1. descarga su instalador a %TEMP% y lo comprueba contra el SHA256SUMS.txt del release
///     (sin sumas o si no cuadran, no se ejecuta y se borra);
///  2. lo ejecuta en silencio, sin repetir las tareas que piden administrador (firewall,
///     RetroArch, inicio de sesión automático); el instalador cierra ModoSofá, sustituye los
///     ficheros y lo vuelve a abrir, conservando configuración y vinculaciones.
///
/// Nunca en modo sofá: se espera a volver al modo trabajo. Con Actualizaciones.InstalarSolo en
/// false solo avisa (bandeja, móvil, «Acerca de») y se instala con «Actualizar ahora».
/// </summary>
public sealed class ActualizacionServicio(
    Configuracion config, ModoServicio modo, IHostApplicationLifetime vida, ILogger<ActualizacionServicio> log)
    : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);
    private static readonly string RutaEstado = Path.Combine(Rutas.Local, "actualizacion.json");
    private static readonly string CarpetaDescargas = Path.Combine(Path.GetTempPath(), "ModoSofa-actualizacion");
    private const string TareasSinElevacion = "/MERGETASKS=\"!firewall,!retroarch,!autologon\"";

    private readonly SemaphoreSlim _instalando = new(1, 1);
    private Estado _estado = LeerEstado();

    /// <summary>Versión más nueva publicada, si la hay.</summary>
    public VersionNueva? Disponible =>
        Versiones.EsMasNueva(_estado.Etiqueta, PanelEstado.Version) && _estado.Pagina is not null
            ? new VersionNueva(_estado.Etiqueta!.TrimStart('v'), _estado.Pagina)
            : null;

    /// <summary>Texto para «Acerca de» y el móvil: al día, buscando, descargando, error…</summary>
    public string Situacion { get; private set; } = "Sin comprobar todavía.";

    /// <summary>Se dispara con versión nueva o cambio de situación.</summary>
    public event Action? Cambio;

    /// <summary>Se dispara la primera vez que se conoce una versión nueva (para el globo de la bandeja).</summary>
    public event Action<VersionNueva>? Nueva;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (Disponible is { } recordada) Fijar($"Hay una versión nueva: {recordada.Version}.");
        if (!config.Actualizaciones.Comprobar) return;

        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            await BuscarAsync(forzar: false, ct);
            if (Disponible is not null && config.Actualizaciones.InstalarSolo)
            {
                if (modo.Modo == Modo.Trabajo)
                    await IntentarInstalarAsync(ct);
                else
                    Fijar($"Hay una versión nueva ({Disponible.Version}); se instalará al volver al modo trabajo.");
            }
            // Cada hora: la pregunta a GitHub sigue siendo diaria, pero así una actualización
            // aplazada por el modo sofá se instala poco después de volver al trabajo.
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    /// <summary>Pregunta a GitHub. Con <paramref name="forzar"/> salta el intervalo y el ETag («Buscar ahora»).</summary>
    public async Task BuscarAsync(bool forzar, CancellationToken ct = default)
    {
        if (!forzar && DateTimeOffset.UtcNow - _estado.UltimaComprobacion < Intervalo) return;
        Fijar("Buscando versiones nuevas…");
        try
        {
            using var http = Cliente(TimeSpan.FromSeconds(20));
            using var peticion = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Versiones.Repositorio}/releases/latest");
            if (!forzar && !string.IsNullOrEmpty(_estado.Etag)) peticion.Headers.TryAddWithoutValidation("If-None-Match", _estado.Etag);

            using var r = await http.SendAsync(peticion, ct);
            _estado.UltimaComprobacion = DateTimeOffset.UtcNow;

            if (r.StatusCode == HttpStatusCode.NotModified) { Guardar(); FijarResultado(); return; }
            if (r.StatusCode == HttpStatusCode.NotFound) { Olvidar(); Guardar(); FijarResultado(); return; }
            if (!r.IsSuccessStatusCode) { Guardar(); Fijar($"GitHub respondió HTTP {(int)r.StatusCode}."); return; }

            _estado.Etag = r.Headers.ETag?.ToString();
            using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            var raiz = json.RootElement;
            var etiqueta = raiz.GetProperty("tag_name").GetString();

            if (!Versiones.EsMasNueva(etiqueta, PanelEstado.Version)) { Olvidar(); Guardar(); FijarResultado(); return; }

            var nuevaEtiqueta = etiqueta != _estado.Etiqueta;
            _estado.Etiqueta = etiqueta;
            _estado.Pagina = raiz.GetProperty("html_url").GetString();
            _estado.InstaladorUrl = _estado.InstaladorNombre = _estado.SumasUrl = null;
            foreach (var a in raiz.GetProperty("assets").EnumerateArray())
            {
                var nombre = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (nombre.StartsWith("ModoSofa-Setup-", StringComparison.OrdinalIgnoreCase) && nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    (_estado.InstaladorNombre, _estado.InstaladorUrl) = (nombre, url);
                else if (nombre.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    _estado.SumasUrl = url;
            }
            Guardar();
            FijarResultado();
            if (nuevaEtiqueta && Disponible is { } nueva)
            {
                log.LogWarning("Hay una versión nueva de ModoSofá: {Nueva} (instalada: {Actual}).", nueva.Version, PanelEstado.Version);
                Nueva?.Invoke(nueva);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            Fijar($"No se pudo comprobar: {ex.Message}");
            log.LogDebug(ex, "Comprobación de versión nueva.");
        }
    }

    /// <summary>«Actualizar ahora» (bandeja, «Acerca de», móvil). Lanza si no se puede.</summary>
    public async Task InstalarAhoraAsync(CancellationToken ct = default)
    {
        if (Disponible is null) await BuscarAsync(forzar: true, ct);
        if (Disponible is null) throw new InvalidOperationException("Ya tienes la última versión.");
        if (modo.Modo == Modo.Sofa) throw new InvalidOperationException("Vuelve al modo trabajo antes de actualizar.");
        await IntentarInstalarAsync(ct, lanzar: true);
    }

    private async Task IntentarInstalarAsync(CancellationToken ct, bool lanzar = false)
    {
        if (!await _instalando.WaitAsync(0, ct)) return;
        try
        {
            var nueva = Disponible!;
            if (_estado.InstaladorUrl is null || _estado.InstaladorNombre is null || _estado.SumasUrl is null)
                throw new InvalidOperationException($"El release {nueva.Version} no publica instalador o sumas SHA-256; descárgalo desde {nueva.Url}.");

            Fijar($"Descargando ModoSofá {nueva.Version}…");
            Directory.CreateDirectory(CarpetaDescargas);
            var destino = Path.Combine(CarpetaDescargas, _estado.InstaladorNombre);
            using var http = Cliente(TimeSpan.FromMinutes(10));
            await using (var origen = await http.GetStreamAsync(_estado.InstaladorUrl, ct))
            await using (var fichero = File.Create(destino))
                await origen.CopyToAsync(fichero, ct);

            var esperado = SumasSha256.Buscar(await http.GetStringAsync(_estado.SumasUrl, ct), _estado.InstaladorNombre);
            string calculado;
            await using (var f = File.OpenRead(destino))
                calculado = Convert.ToHexStringLower(await SHA256.HashDataAsync(f, ct));
            if (esperado is null || calculado != esperado)
            {
                File.Delete(destino);
                throw new InvalidOperationException("El instalador descargado no coincide con el SHA-256 publicado; se ha borrado.");
            }

            // Última comprobación: entre la descarga y aquí podría haber empezado el modo sofá.
            if (modo.Modo == Modo.Sofa)
            {
                Fijar($"Versión {nueva.Version} descargada; se instalará al volver al modo trabajo.");
                return;
            }

            log.LogInformation("Instalando ModoSofá {Nueva} (SHA-256 verificado). El agente se reiniciará.", nueva.Version);
            Fijar($"Instalando ModoSofá {nueva.Version}…");
            // UseShellExecute: el instalador no es hijo nuestro y sobrevive a que nos cierre.
            Process.Start(new ProcessStartInfo(destino, $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART {TareasSinElevacion}") { UseShellExecute = true });
            vida.StopApplication();
        }
        catch (Exception ex) when (!lanzar && ex is not OperationCanceledException)
        {
            Fijar($"No se pudo actualizar: {ex.Message}");
            log.LogWarning("No se pudo actualizar automáticamente: {Mensaje}", ex.Message);
        }
        finally
        {
            _instalando.Release();
        }
    }

    private void FijarResultado() =>
        Fijar(Disponible is { } d ? $"Hay una versión nueva: {d.Version}." : $"Tienes la última versión ({PanelEstado.Version}).");

    private void Fijar(string situacion)
    {
        Situacion = situacion;
        Cambio?.Invoke();
    }

    private void Olvidar() => _estado.Etiqueta = _estado.Pagina = _estado.InstaladorUrl = _estado.InstaladorNombre = _estado.SumasUrl = null;

    private static HttpClient Cliente(TimeSpan espera)
    {
        var c = new HttpClient { Timeout = espera };
        // api.github.com responde 403 sin User-Agent.
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"ModoSofa/{PanelEstado.Version}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    private void Guardar()
    {
        try { Ficheros.GuardarJson(RutaEstado, _estado); }
        catch (Exception ex) { log.LogDebug(ex, "No se pudo guardar el estado de actualización."); }
    }

    private static Estado LeerEstado()
    {
        try { return Ficheros.CargarJson<Estado>(RutaEstado); }
        catch { return new Estado(); }
    }

    /// <summary>Memoria entre arranques: el aviso sigue en pie aunque hoy no toque preguntar.</summary>
    private sealed class Estado
    {
        public DateTimeOffset UltimaComprobacion { get; set; }
        public string? Etag { get; set; }
        public string? Etiqueta { get; set; }
        public string? Pagina { get; set; }
        public string? InstaladorUrl { get; set; }
        public string? InstaladorNombre { get; set; }
        public string? SumasUrl { get; set; }
    }
}
