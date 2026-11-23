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

using System.Diagnostics;
using CapicuaSoft.ModoSofa.Agente.Efectos;
using CapicuaSoft.ModoSofa.Agente.Infraestructura;
using CapicuaSoft.ModoSofa.Agente.Win32;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Servicios;

/// <summary>
/// Entrar y salir del modo sofá. Cada paso es independiente: si uno falla se registra y se sigue,
/// y todo lo que se cambia se apunta en el estado persistente antes de cambiarlo, para poder
/// deshacerlo aunque el agente se caiga a mitad.
/// </summary>
public sealed class ModoServicio(
    Configuracion config,
    AlmacenEstado almacen,
    Pantallas pantallas,
    Audio audio,
    Ventanas ventanas,
    Energia energia,
    Lanzadores lanzadores,
    Procesos procesos,
    TeleLg tele,
    ILogger<ModoServicio> log)
{
    /// <summary>Procesos cuyas ventanas no se minimizan nunca (los lanzadores del modo sofá).</summary>
    private static readonly HashSet<string> NoMinimizar = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamwebhelper", "ES-DE", "retroarch",
    };

    public Modo Modo => almacen.Valor.Modo;
    public DateTimeOffset? Desde => almacen.Valor.Desde;

    /// <summary>El guardián se puede pausar desde el overlay o el móvil sin tocar la configuración.</summary>
    public bool GuardianActivo { get; set; } = true;

    public IReadOnlyCollection<int> ProcesosTrabajo => almacen.Valor.ProcesosTrabajo;

    public event Action? Cambio;

    public async Task EntrarAsync(CancellationToken ct = default)
    {
        var e = almacen.Valor;
        if (e.Modo == Modo.Sofa)
        {
            log.LogInformation("Ya está en modo sofá.");
            return;
        }

        log.LogInformation("Entrando en modo sofá…");
        var reloj = Stopwatch.StartNew();
        e.Limpiar();
        e.Modo = Modo.Sofa;
        e.Desde = DateTimeOffset.Now;
        GuardianActivo = true;
        almacen.Guardar();
        Cambio?.Invoke();

        if (tele.Configurada)
            await PasoAsync("Tele", () => tele.PrepararAsync(ct));

        var fragmentoTele = config.Tele.NombrePantalla;
        await PasoAsync("Pantallas", async () =>
        {
            if (!pantallas.EstaConectada(fragmentoTele))
            {
                log.LogWarning("La tele («{Fragmento}») no está conectada; no se cambian las pantallas.", fragmentoTele);
                return;
            }
            e.PantallasAnteriores = pantallas.Guardar();
            almacen.Guardar();

            // Lo normal es enchufar el cable y después encender la tele y buscar su entrada HDMI.
            // Hasta que la tele muestra la entrada del PC, Windows la ve conectada pero no puede
            // activarla (ERROR_BAD_CONFIGURATION, 1610): se reintenta durante un rato.
            var limite = DateTime.UtcNow.AddSeconds(config.Tele.EsperaImagenS);
            var avisado = false;
            while (!pantallas.SoloPantalla(fragmentoTele, avisar: false))
            {
                if (DateTime.UtcNow > limite || !pantallas.EstaConectada(fragmentoTele))
                {
                    pantallas.SoloPantalla(fragmentoTele); // último intento, ya con sus avisos en el log
                    log.LogWarning("La tele no aceptó la imagen del PC en {Segundos} s: ¿está encendida y en la entrada HDMI del PC?", config.Tele.EsperaImagenS);
                    return;
                }
                if (!avisado)
                {
                    log.LogInformation("Esperando a que la tele muestre la entrada HDMI del PC…");
                    avisado = true;
                }
                await Task.Delay(3000, ct);
            }
            await Task.Delay(1500, ct); // que Windows termine de recolocar el escritorio
        });

        if (!string.IsNullOrWhiteSpace(config.Audio.DispositivoSofa))
        {
            await PasoAsync("Audio", async () =>
            {
                e.AudioAnterior = audio.SalidaActual();
                e.VolumenAnterior = audio.Volumen();
                almacen.Guardar();
                // Windows crea la salida de audio HDMI de la tele unos segundos después de la imagen
                // (comprobado con la LG: no existía 2 s después de detectar la pantalla).
                var fragmento = config.Audio.DispositivoSofa;
                for (var i = 0; i < 20 && !audio.Existe(fragmento); i++) await Task.Delay(1000, ct);
                if (audio.CambiarA(fragmento) && config.Audio.VolumenInicialSofa >= 0)
                    audio.FijarVolumen(config.Audio.VolumenInicialSofa / 100f);
            });
        }

        if (config.Ventanas.CerrarProcesos.Count > 0)
        {
            await PasoAsync("Cerrar programas", async () =>
            {
                e.ProcesosCerrados = await procesos.CerrarListaAsync();
                almacen.Guardar();
            });
        }

        if (config.Ventanas.Minimizar)
        {
            await PasoAsync("Minimizar ventanas", () =>
            {
                var excluidos = new HashSet<string>(config.Ventanas.NoMinimizar, StringComparer.OrdinalIgnoreCase);
                var lista = ventanas.DeUsuario()
                    .Where(v => !NoMinimizar.Contains(v.Proceso) && !excluidos.Contains(v.Proceso))
                    .ToList();
                foreach (var v in lista)
                    e.Ventanas.Add(new VentanaGuardada(v.Hwnd, v.ProcesoId, v.Proceso, v.Titulo, Ventanas.GuardarPosicion(v.Hwnd)));
                e.ProcesosTrabajo = lista.Select(v => v.ProcesoId).Distinct().ToList();
                almacen.Guardar();
                foreach (var v in lista) ventanas.Minimizar(v.Hwnd);
                log.LogInformation("{Cantidad} ventanas minimizadas.", lista.Count);
                return Task.CompletedTask;
            });
        }

        await PasoAsync("Energía", () =>
        {
            if (!string.IsNullOrWhiteSpace(config.Energia.PlanSofa))
            {
                e.PlanEnergiaAnterior = energia.PlanActivo();
                almacen.Guardar();
                energia.ActivarPlan(config.Energia.PlanSofa);
            }
            if (config.Energia.EvitarSuspension) energia.EvitarSuspension();
            return Task.CompletedTask;
        });

        await PasoAsync("Lanzador", () => config.Lanzadores.AbrirEsdeAlEntrar
            ? lanzadores.AbrirEsDeAsync(ct)
            : lanzadores.BigPictureAsync(ct));

        log.LogInformation("Modo sofá activo ({Segundos:0.0} s).", reloj.Elapsed.TotalSeconds);
        Cambio?.Invoke();
    }

    public async Task SalirAsync(CancellationToken ct = default)
    {
        var e = almacen.Valor;
        if (e.Modo == Modo.Trabajo)
        {
            log.LogInformation("Ya está en modo trabajo.");
            return;
        }

        log.LogInformation("Volviendo al modo trabajo…");
        var reloj = Stopwatch.StartNew();
        // El guardián no debe apartar ventanas mientras se restauran.
        GuardianActivo = false;
        energia.PermitirSuspension();

        if (config.Lanzadores.CerrarSteamAlSalir)
        {
            await PasoAsync("Cerrar ES-DE", () => CerrarEsDeAsync());
            await PasoAsync("Cerrar Steam", () => lanzadores.CerrarSteamAsync());
        }

        if (e.PlanEnergiaAnterior is { } plan)
            await PasoAsync("Energía", () => { energia.ActivarPlan(plan); return Task.CompletedTask; });

        // Siempre, aunque no se guardara nada: si la entrada falló antes de guardar (la tele no
        // aceptaba la imagen), Restaurar(null) extiende a todas las pantallas y no deja un monitor
        // apagado (pasó en las pruebas).
        await PasoAsync("Pantallas", async () =>
        {
            pantallas.Restaurar(e.PantallasAnteriores);
            await Task.Delay(2000, ct); // las ventanas se recolocan contra la topología restaurada
        });

        if (e.AudioAnterior is { } salida)
        {
            await PasoAsync("Audio", () =>
            {
                audio.CambiarPorId(salida);
                if (e.VolumenAnterior is { } v) audio.FijarVolumen(v);
                log.LogInformation("Audio restaurado: {Nombre}.", audio.NombreActual());
                return Task.CompletedTask;
            });
        }

        if (e.Ventanas.Count > 0)
        {
            await PasoAsync("Restaurar ventanas", () =>
            {
                var restauradas = e.Ventanas.Count(v => ventanas.Restaurar(v.Hwnd, v.ProcesoId, v.Posicion));
                log.LogInformation("{Restauradas} de {Total} ventanas restauradas.", restauradas, e.Ventanas.Count);
                return Task.CompletedTask;
            });
        }

        if (config.Ventanas.RelanzarAlSalir && e.ProcesosCerrados.Count > 0)
            await PasoAsync("Relanzar programas", () => { procesos.Relanzar(e.ProcesosCerrados); return Task.CompletedTask; });

        if (tele.Configurada && config.Tele.ApagarAlSalir)
            await PasoAsync("Apagar tele", () => tele.ApagarAsync(ct));

        e.Limpiar();
        almacen.Guardar();
        log.LogInformation("Modo trabajo ({Segundos:0.0} s).", reloj.Elapsed.TotalSeconds);
        Cambio?.Invoke();
    }

    /// <summary>
    /// Si el agente arranca en modo sofá (cuelgue, reinicio), sigue en él si la tele está
    /// conectada y, si no, deshace lo que quedó apuntado.
    /// </summary>
    public async Task RecuperarAlArrancarAsync()
    {
        if (almacen.Valor.Modo != Modo.Sofa) return;
        if (pantallas.EstaConectada(config.Tele.NombrePantalla))
        {
            log.LogInformation("Arranque en modo sofá con la tele conectada: se continúa.");
            if (config.Energia.EvitarSuspension) energia.EvitarSuspension();
            return;
        }
        log.LogWarning("Arranque en modo sofá sin la tele: se vuelve al modo trabajo.");
        await SalirAsync();
    }

    private async Task CerrarEsDeAsync()
    {
        foreach (var p in Process.GetProcessesByName("ES-DE"))
        {
            p.CloseMainWindow();
            if (!p.WaitForExit(5000))
                try { p.Kill(); } catch { /* ya cerrado */ }
        }
        await Task.CompletedTask;
    }

    private async Task PasoAsync(string nombre, Func<Task> paso)
    {
        try
        {
            await paso();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Paso «{Paso}» fallido: {Mensaje}", nombre, ex.Message);
        }
    }
}
