using System.Diagnostics;
using ModoSofa.Agente.Efectos;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Servicios;

/// <summary>Todas las acciones de ModoSofá, en el orden en que se muestran.</summary>
public static class CatalogoAcciones
{
    public static void Registrar(IServiceProvider sp)
    {
        var r = sp.GetRequiredService<RegistroAcciones>();
        var config = sp.GetRequiredService<Configuracion>();
        var modo = sp.GetRequiredService<ModoServicio>();
        var lanzadores = sp.GetRequiredService<Lanzadores>();
        var audio = sp.GetRequiredService<Audio>();
        var tele = sp.GetRequiredService<TeleLg>();
        var overlay = sp.GetRequiredService<IOverlay>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ModoSofa.Acciones");

        r.Registrar(new Accion("modo.sofa", "Activar modo sofá", "Modo", ct => modo.EntrarAsync(ct)));
        r.Registrar(new Accion("modo.trabajo", "Volver a trabajar", "Modo", ct => modo.SalirAsync(ct)));

        r.Registrar(new Accion("juego.cerrar", "Cerrar juego", "Juego", _ => lanzadores.CerrarJuegoAsync()));
        r.Registrar(new Accion("juego.enfocar", "Enfocar juego", "Juego", _ => Sincrono(lanzadores.EnfocarActivo)));

        r.Registrar(new Accion("volumen.subir", "Subir volumen", "Volumen",
            _ => Sincrono(() => audio.Pasos(config.Audio.Pasos)), Exclusiva: false));
        r.Registrar(new Accion("volumen.bajar", "Bajar volumen", "Volumen",
            _ => Sincrono(() => audio.Pasos(-config.Audio.Pasos)), Exclusiva: false));
        r.Registrar(new Accion("volumen.silenciar", "Silenciar", "Volumen",
            _ => Sincrono(() => log.LogInformation(audio.AlternarSilencio() ? "Silenciado." : "Sonido activado.")), Exclusiva: false));

        r.Registrar(new Accion("steam.bigpicture", "Steam Big Picture", "Lanzadores", ct => lanzadores.BigPictureAsync(ct)));
        r.Registrar(new Accion("esde.abrir", "EmulationStation", "Lanzadores", ct => lanzadores.AbrirEsDeAsync(ct)));
        r.Registrar(new Accion("steam.cerrar", "Cerrar Steam", "Lanzadores", _ => lanzadores.CerrarSteamAsync()));

        r.Registrar(new Accion("overlay.alternar", "Menú en la tele", "Sofá", _ => Sincrono(overlay.Alternar), Exclusiva: false));
        r.Registrar(new Accion("guardian.alternar", "Guardián de foco", "Sofá", _ => Sincrono(() =>
        {
            modo.GuardianActivo = !modo.GuardianActivo;
            log.LogInformation("Guardián de foco {Estado}.", modo.GuardianActivo ? "activado" : "en pausa");
        }), Exclusiva: false));

        r.Registrar(new Accion("tele.encender", "Encender tele", "Tele", ct => tele.EncenderAsync(ct)));
        r.Registrar(new Accion("tele.apagar", "Apagar tele", "Tele", ct => tele.ApagarAsync(ct)));

        r.Registrar(new Accion("equipo.suspender", "Suspender", "Equipo", _ => Sincrono(() =>
            Application.SetSuspendState(PowerState.Suspend, force: false, disableWakeEvent: false)), Peligrosa: true));
        r.Registrar(new Accion("equipo.reiniciar", "Reiniciar", "Equipo", _ => Apagado("/r /t 0"), Peligrosa: true));
        r.Registrar(new Accion("equipo.apagar", "Apagar", "Equipo", _ => Apagado("/s /t 0"), Peligrosa: true));
    }

    private static Task Sincrono(Action accion)
    {
        accion();
        return Task.CompletedTask;
    }

    private static Task Apagado(string argumentos)
    {
        Process.Start(new ProcessStartInfo("shutdown", argumentos) { UseShellExecute = false, CreateNoWindow = true });
        return Task.CompletedTask;
    }
}
