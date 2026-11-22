namespace ModoSofa.Nucleo;

/// <summary>Botones del mando con los mismos bits que XINPUT_GAMEPAD.wButtons.</summary>
[Flags]
public enum Botones : ushort
{
    Ninguno = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LB = 0x0100,
    RB = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

public static class Combos
{
    /// <summary>"Back+Start+B" → máscara. Lanza si algún nombre no existe.</summary>
    public static Botones Parsear(string texto)
    {
        var resultado = Botones.Ninguno;
        foreach (var parte in texto.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Botones>(parte, ignoreCase: true, out var boton) || boton == Botones.Ninguno)
                throw new FormatException($"Botón desconocido en «{texto}»: {parte}");
            resultado |= boton;
        }
        if (resultado == Botones.Ninguno) throw new FormatException("Combinación vacía.");
        return resultado;
    }

    public static string Nombre(Botones botones) => botones.ToString().Replace(", ", "+");
}

public abstract record EventoMando;
public sealed record AlternarOverlay : EventoMando;
public sealed record Atajo(string AccionId) : EventoMando;
public sealed record Pulsado(Botones Boton) : EventoMando;

/// <summary>
/// Convierte lecturas sucesivas del mando en eventos. Las combinaciones deben pulsarse
/// <b>exactas</b>: L1+R1+Start+Select (menú de RetroArch) no dispara Back+Start. Tras disparar
/// algo, no se vuelve a disparar nada hasta soltar todos los botones, para que pasar de
/// Back+Start+B a Back+Start no abra además el overlay.
/// </summary>
public sealed class DetectorCombos
{
    private readonly Botones _overlay;
    private readonly long _mantenerMs;
    private readonly Dictionary<Botones, string> _atajos;
    private Botones _anterior;
    private long _inicioMantener = -1;
    private bool _bloqueado;

    public DetectorCombos(Botones overlay, int mantenerMs, IReadOnlyDictionary<Botones, string> atajos)
    {
        _overlay = overlay;
        _mantenerMs = mantenerMs;
        _atajos = new Dictionary<Botones, string>(atajos);
    }

    public static DetectorCombos DesdeConfig(MandoConfig config) => new(
        Combos.Parsear(config.ComboOverlay),
        config.MantenerMs,
        config.Atajos.ToDictionary(kv => Combos.Parsear(kv.Key), kv => kv.Value));

    public List<EventoMando> Procesar(Botones botones, long ahoraMs)
    {
        var eventos = new List<EventoMando>();

        var nuevos = botones & ~_anterior;
        foreach (Botones boton in Enum.GetValues<Botones>())
            if (boton != Botones.Ninguno && nuevos.HasFlag(boton))
                eventos.Add(new Pulsado(boton));

        if (botones == Botones.Ninguno) _bloqueado = false;

        if (!_bloqueado)
        {
            if (botones != _anterior && _atajos.TryGetValue(botones, out var accion))
            {
                eventos.Add(new Atajo(accion));
                _bloqueado = true;
                _inicioMantener = -1;
            }
            else if (botones == _overlay)
            {
                if (_inicioMantener < 0) _inicioMantener = ahoraMs;
                if (ahoraMs - _inicioMantener >= _mantenerMs)
                {
                    eventos.Add(new AlternarOverlay());
                    _bloqueado = true;
                    _inicioMantener = -1;
                }
            }
            else
            {
                _inicioMantener = -1;
            }
        }

        _anterior = botones;
        return eventos;
    }
}
