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
using Microsoft.Win32;
using CapicuaSoft.ModoSofa.Agente.Efectos;
using CapicuaSoft.ModoSofa.Agente.Infraestructura;
using CapicuaSoft.ModoSofa.Agente.Servicios;
using CapicuaSoft.ModoSofa.Nucleo;

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

public interface IOverlay
{
    bool Visible { get; }

    /// <summary>Lo que el usuario tiene delante: la ventana anterior al menú si está abierto, o la activa.</summary>
    IntPtr VentanaDelante { get; }

    void Alternar();
    void Boton(Botones boton);
}

/// <summary>
/// Hilo STA con su bucle de mensajes: icono de la bandeja, overlay de la tele y ventana de
/// vinculación. Todo lo que llega de otros hilos se pasa a este con el contexto de sincronización.
/// </summary>
public sealed class Bandeja(
    Configuracion config, AlmacenSecretos secretos, RegistroAcciones registro, ModoServicio modo,
    RetroArch retroArch, IHostApplicationLifetime vida, IServiceProvider servicios, ILogger<Bandeja> log)
    : IHostedService, IOverlay
{
    private const string ClaveInicio = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NombreInicio = "ModoSofa";

    private Thread? _hilo;
    private SynchronizationContext? _ui;
    private ApplicationContext? _contexto;
    private NotifyIcon? _icono;
    private ToolStripMenuItem? _titulo, _alternarModo, _actualizar;
    // Se resuelve al arrancar y no en el constructor: ActualizacionServicio no depende de la bandeja,
    // pero así queda claro que la bandeja tampoco lo necesita para existir.
    private ActualizacionServicio? _actualizacion;
    private VentanaOverlay? _overlay;
    private VentanaEspera? _espera;
    private bool _pausamosRetroArch;

    public bool Visible => _overlay?.Visible ?? false;

    public IntPtr VentanaDelante => Visible && _overlay!.Anterior != IntPtr.Zero ? _overlay.Anterior : Win32.Nativo.GetForegroundWindow();

    public Task StartAsync(CancellationToken ct)
    {
        var listo = new TaskCompletionSource();
        _actualizacion = servicios.GetRequiredService<ActualizacionServicio>();
        _hilo = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _ui = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_ui);
            CrearIcono();
            _overlay = new VentanaOverlay(config.Puerto, secretos.Valor.TokenApi, Rutas.WebView2, Ocultar);
            _contexto = new ApplicationContext();
            // «ModoSofa.exe --acerca-de» abre «Acerca de» al arrancar (útil para probarla).
            if (Environment.GetCommandLineArgs().Contains("--acerca-de")) AcercaDe.Mostrar(_actualizacion!);
            listo.SetResult();
            Application.Run(_contexto);
            _icono!.Visible = false;
            _icono.Dispose();
        }) { Name = "ModoSofa.Interfaz", IsBackground = true };
        _hilo.SetApartmentState(ApartmentState.STA);
        _hilo.Start();

        modo.Cambio += () => EnUi(ActualizarIcono);
        modo.Progreso += paso => EnUi(() => Espera(paso));
        _actualizacion.Cambio += () => EnUi(ActualizarIcono);
        _actualizacion.Nueva += v => EnUi(() => _icono!.ShowBalloonTip(10000, $"ModoSofá {v.Version} disponible",
            config.Actualizaciones.InstalarSolo
                ? "Se instalará sola en modo trabajo. Pulsa para ver los detalles."
                : "Pulsa para actualizar.", ToolTipIcon.Info));
        return listo.Task;
    }

    public Task StopAsync(CancellationToken ct)
    {
        EnUi(() => _contexto?.ExitThread());
        _hilo?.Join(3000);
        return Task.CompletedTask;
    }

    // --- Overlay ---------------------------------------------------------------------------

    public void Alternar() => EnUi(() =>
    {
        if (Visible) Ocultar();
        else Mostrar();
    });

    public void Boton(Botones boton) => EnUi(() => _overlay?.EnviarBoton(boton));

    private void Mostrar()
    {
        _overlay!.Mostrar();
        if (config.Mando.PausarRetroArchConOverlay) _ = PausarRetroArchAsync();
    }

    private void Ocultar()
    {
        _overlay!.Ocultar();
        if (_pausamosRetroArch)
        {
            _pausamosRetroArch = false;
            _ = retroArch.EnviarAsync("PAUSE_TOGGLE");
        }
    }

    private async Task PausarRetroArchAsync()
    {
        try
        {
            if (!retroArch.Abierto() || await retroArch.EstadoAsync() != "PLAYING") return;
            await retroArch.EnviarAsync("PAUSE_TOGGLE");
            _pausamosRetroArch = true;
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Pausa de RetroArch.");
        }
    }

    // --- Pantalla de espera ----------------------------------------------------------------

    private void Espera(string? paso)
    {
        if (paso is null)
        {
            _espera?.Ocultar();
            return;
        }
        if (!config.Lanzadores.PantallaEspera) return;
        _espera ??= new VentanaEspera();
        _espera.Mostrar(paso);
    }

    // --- Icono -----------------------------------------------------------------------------

    private void CrearIcono()
    {
        _titulo = new ToolStripMenuItem("ModoSofá") { Enabled = false, Image = MenuIconos.Sofa };
        _alternarModo = new ToolStripMenuItem("Activar modo sofá", null, (_, _) =>
            registro.Lanzar(modo.Modo == Modo.Sofa ? "modo.trabajo" : "modo.sofa", "bandeja"));

        var inicio = new ToolStripMenuItem("Iniciar con Windows") { Checked = IniciaConWindows(), CheckOnClick = true };
        inicio.CheckedChanged += (_, _) => FijarInicioConWindows(inicio.Checked);

        // Como en SilentRunner: iconos de 32 px que el menú reescala según el DPI.
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            _titulo,
            new ToolStripSeparator(),
            _alternarModo,
            new ToolStripMenuItem("Mostrar menú en la tele", MenuIconos.Tele, (_, _) => Alternar()),
            new ToolStripMenuItem("Cerrar juego", MenuIconos.Detener, (_, _) => registro.Lanzar("juego.cerrar", "bandeja")),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Vincular móvil…", MenuIconos.Movil, (_, _) => new VentanaVincular(config.Puerto, secretos.Valor.TokenApi).Show()),
            new ToolStripMenuItem("Abrir panel en el navegador", MenuIconos.Globo, (_, _) => AbrirPanel()),
            new ToolStripMenuItem("Copiar token de Alexa", MenuIconos.Llave, (_, _) => Clipboard.SetText(secretos.Valor.TokenAlexa)),
            new ToolStripMenuItem("Abrir carpeta de configuración", MenuIconos.Carpeta, (_, _) =>
                Process.Start(new ProcessStartInfo(Rutas.Datos) { UseShellExecute = true })),
            new ToolStripMenuItem("Abrir carpeta de logs", MenuIconos.Documento, (_, _) =>
                Process.Start(new ProcessStartInfo(Rutas.Logs) { UseShellExecute = true })),
            inicio,
            new ToolStripSeparator(),
            _actualizar = new ToolStripMenuItem("Actualizar", MenuIconos.Actualizar, (_, _) => AcercaDe.Mostrar(_actualizacion!)) { Visible = false, Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) },
            new ToolStripMenuItem("Acerca de ModoSofá", MenuIconos.Informacion, (_, _) => AcercaDe.Mostrar(_actualizacion!)),
            new ToolStripMenuItem("Invitar a un café", MenuIconos.Corazon, (_, _) => Enlaces.Abrir(Enlaces.Donacion)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Salir", MenuIconos.Salir, (_, _) => vida.StopApplication()),
        ]);

        _icono = new NotifyIcon
        {
            Icon = Icono.Crear(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icono.DoubleClick += (_, _) => AbrirPanel();
        _icono.BalloonTipClicked += (_, _) => AcercaDe.Mostrar(_actualizacion!);
        ActualizarIcono();
    }

    private void ActualizarIcono()
    {
        var sofa = modo.Modo == Modo.Sofa;
        var texto = sofa ? "ModoSofá — modo sofá" : "ModoSofá — modo trabajo";
        _icono!.Text = texto;
        _titulo!.Text = texto;
        _alternarModo!.Text = sofa ? "Volver a trabajar" : "Activar modo sofá";
        _alternarModo.Image = sofa ? MenuIconos.Trabajo : MenuIconos.Sofa;
        _titulo.Image = sofa ? MenuIconos.Sofa : MenuIconos.Trabajo;
        var nueva = _actualizacion?.Disponible;
        _actualizar!.Visible = nueva is not null;
        if (nueva is not null) _actualizar.Text = $"Hay una versión nueva: {nueva.Version}";
    }

    private void AbrirPanel() =>
        Process.Start(new ProcessStartInfo($"http://localhost:{config.Puerto}/#t={secretos.Valor.TokenApi}") { UseShellExecute = true });

    private static bool IniciaConWindows() => Registry.GetValue(ClaveInicio, NombreInicio, null) is string;

    private void FijarInicioConWindows(bool activar)
    {
        using var clave = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)!;
        if (activar) clave.SetValue(NombreInicio, $"\"{Environment.ProcessPath}\"");
        else clave.DeleteValue(NombreInicio, throwOnMissingValue: false);
        log.LogInformation("Inicio con Windows {Estado}.", activar ? "activado" : "desactivado");
    }

    private void EnUi(Action accion)
    {
        if (_ui is null) return;
        _ui.Post(_ =>
        {
            try { accion(); }
            catch (Exception ex) { log.LogWarning(ex, "Error en la interfaz."); }
        }, null);
    }
}
