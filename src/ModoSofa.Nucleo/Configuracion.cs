using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModoSofa.Nucleo;

/// <summary>
/// Toda la configuración de ModoSofá. Vive en <c>%APPDATA%\ModoSofa\config.json</c>; si falta,
/// se crea con estos valores por defecto. Los fragmentos de nombre se comparan sin distinguir
/// mayúsculas.
/// </summary>
public sealed class Configuracion
{
    /// <summary>Puerto HTTP del agente (móvil, overlay, Alexa vía Tailscale).</summary>
    public int Puerto { get; set; } = 5790;

    /// <summary>Si es false, solo escucha en 127.0.0.1 (el móvil tendría que entrar por Tailscale serve).</summary>
    public bool EscucharEnRed { get; set; } = true;

    public TeleConfig Tele { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
    public VentanasConfig Ventanas { get; set; } = new();
    public EnergiaConfig Energia { get; set; } = new();
    public LanzadoresConfig Lanzadores { get; set; } = new();
    public MandoConfig Mando { get; set; } = new();
    public GuardianConfig Guardian { get; set; } = new();
    public AlexaConfig Alexa { get; set; } = new();
    public ActualizacionesConfig Actualizaciones { get; set; } = new();

    public static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Carga el fichero; si no existe lo crea con los valores por defecto.</summary>
    public static Configuracion Cargar(string ruta)
    {
        if (!File.Exists(ruta))
        {
            var porDefecto = new Configuracion();
            porDefecto.Guardar(ruta);
            return porDefecto;
        }

        var json = File.ReadAllText(ruta);
        return Deserializar(json);
    }

    public static Configuracion Deserializar(string json) =>
        JsonSerializer.Deserialize<Configuracion>(json, OpcionesJson)
        ?? throw new InvalidDataException("La configuración está vacía.");

    public string Serializar() => JsonSerializer.Serialize(this, OpcionesJson);

    public void Guardar(string ruta) => Ficheros.EscribirAtomico(ruta, Serializar());
}

public sealed class TeleConfig
{
    /// <summary>Fragmento del nombre que la tele anuncia por EDID (ver /api/diagnostico).</summary>
    public string NombrePantalla { get; set; } = "LG TV";

    /// <summary>Entrar en modo sofá en cuanto se detecta la tele conectada.</summary>
    public bool EntrarAlConectar { get; set; } = true;

    /// <summary>Volver al modo trabajo cuando la tele desaparece (se desenchufa el cable).</summary>
    public bool SalirAlDesconectar { get; set; } = true;

    /// <summary>Tiempo que el cambio debe mantenerse antes de reaccionar (anti-rebote del HDMI).</summary>
    public int EsperaDeteccionMs { get; set; } = 4000;

    /// <summary>Controlar la tele LG webOS por red (fase 4).</summary>
    public bool ControlarLg { get; set; } = false;
    public string LgIp { get; set; } = "";
    /// <summary>MAC de la tele para encenderla por Wake-on-LAN («Encender con Wi-Fi/LAN» activado en la tele).</summary>
    public string LgMac { get; set; } = "";
    /// <summary>Entrada a la que cambiar al entrar en modo sofá (HDMI_1, HDMI_2…). Vacío: no cambiar.</summary>
    public string LgEntrada { get; set; } = "HDMI_1";
    public bool EncenderAlEntrar { get; set; } = true;
    public bool ApagarAlSalir { get; set; } = true;
}

public sealed class AudioConfig
{
    /// <summary>Fragmento del nombre del dispositivo de salida de la tele. Vacío: no cambiar el audio.</summary>
    public string DispositivoSofa { get; set; } = "LG TV";

    /// <summary>Volumen (0-100) al entrar en modo sofá; -1 para no tocarlo.</summary>
    public int VolumenInicialSofa { get; set; } = -1;

    /// <summary>Pasos por cada orden de subir/bajar volumen.</summary>
    public int Pasos { get; set; } = 2;
}

public sealed class VentanasConfig
{
    /// <summary>Minimizar todas las ventanas de trabajo al entrar (y restaurarlas al salir).</summary>
    public bool Minimizar { get; set; } = true;

    /// <summary>Procesos (sin .exe) que se cierran al entrar.</summary>
    public List<string> CerrarProcesos { get; set; } = ["ms-teams", "Teams", "OUTLOOK", "olk"];

    /// <summary>Si sigue vivo tras <see cref="EsperaCierreMs"/>, se termina el proceso.</summary>
    public bool ForzarCierre { get; set; } = true;
    public int EsperaCierreMs { get; set; } = 5000;

    /// <summary>Volver a abrir al salir los procesos que se cerraron al entrar.</summary>
    public bool RelanzarAlSalir { get; set; } = true;

    /// <summary>Procesos cuyas ventanas nunca se minimizan.</summary>
    public List<string> NoMinimizar { get; set; } = [];
}

public sealed class EnergiaConfig
{
    /// <summary>GUID del plan de energía para jugar (powercfg /list). Vacío: no cambiar.</summary>
    public string PlanSofa { get; set; } = "";

    /// <summary>Impedir que el PC o la pantalla se suspendan mientras dura el modo sofá.</summary>
    public bool EvitarSuspension { get; set; } = true;
}

public sealed class LanzadoresConfig
{
    /// <summary>Ruta de steam.exe. Vacío: se lee del registro.</summary>
    public string SteamExe { get; set; } = "";
    public bool CerrarSteamAlSalir { get; set; } = true;

    public string EsdeExe { get; set; } = @"C:\Games\EmulationStation\ES-DE.exe";
    /// <summary>Abrir ES-DE directamente al entrar (si no, se entra en Steam Big Picture).</summary>
    public bool AbrirEsdeAlEntrar { get; set; } = false;

    public int RetroArchPuerto { get; set; } = 55355;

    /// <summary>Fragmentos de ruta que identifican el ejecutable de un juego.</summary>
    public List<string> PatronesRutaJuegos { get; set; } = [@"steamapps\common", @"C:\Games"];

    /// <summary>Procesos que nunca se consideran «el juego» (se suman a la lista interna).</summary>
    public List<string> ProcesosNoJuego { get; set; } = [];
}

public sealed class MandoConfig
{
    public bool Activado { get; set; } = true;

    /// <summary>Combinación que, mantenida, abre o cierra el overlay.</summary>
    public string ComboOverlay { get; set; } = "Back+Start";
    public int MantenerMs { get; set; } = 800;

    /// <summary>Combinación exacta → id de acción.</summary>
    public Dictionary<string, string> Atajos { get; set; } = new()
    {
        ["Back+Start+B"] = "juego.cerrar",
        ["Back+Start+Y"] = "juego.enfocar",
        ["Back+Start+X"] = "volumen.silenciar",
        ["Back+Start+DPadUp"] = "volumen.subir",
        ["Back+Start+DPadDown"] = "volumen.bajar",
    };

    /// <summary>Pausar RetroArch mientras el overlay está abierto.</summary>
    public bool PausarRetroArchConOverlay { get; set; } = true;
}

public sealed class GuardianConfig
{
    /// <summary>Durante el modo sofá, si una ventana de trabajo se pone delante, se minimiza.</summary>
    public bool Activado { get; set; } = true;
    public int IntervaloMs { get; set; } = 750;
}

public sealed class ActualizacionesConfig
{
    /// <summary>Preguntar a GitHub una vez al día si hay un release más nuevo. Solo avisa: nunca instala.</summary>
    public bool Comprobar { get; set; } = true;
}

public sealed class AlexaConfig
{
    /// <summary>Aceptar órdenes en /api/alexa (con su propio token).</summary>
    public bool Activada { get; set; } = true;
}
