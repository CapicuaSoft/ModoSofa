namespace ModoSofa.Nucleo;

/// <summary>
/// Una acción con nombre. El mando, el móvil, el overlay, la bandeja y Alexa invocan siempre a
/// través del registro, nunca a los efectos directamente.
/// </summary>
/// <param name="Exclusiva">Las exclusivas (cambios de modo, cerrar juego…) se ejecutan de una en
/// una; las demás (volumen) no esperan.</param>
public sealed record Accion(
    string Id,
    string Nombre,
    string Grupo,
    Func<CancellationToken, Task> Ejecutar,
    bool Peligrosa = false,
    bool Exclusiva = true);

public sealed record ResultadoAccion(string Id, bool Correcta, string? Error, TimeSpan Duracion);

public sealed class RegistroAcciones
{
    private readonly Dictionary<string, Accion> _acciones = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _orden = [];
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    /// <summary>Se dispara al empezar una acción (id, origen).</summary>
    public event Action<string, string>? Iniciada;

    /// <summary>Se dispara al terminar una acción.</summary>
    public event Action<ResultadoAccion, string>? Terminada;

    public void Registrar(Accion accion)
    {
        if (!_acciones.TryAdd(accion.Id, accion))
            throw new InvalidOperationException($"Acción duplicada: {accion.Id}");
        _orden.Add(accion.Id);
    }

    public IReadOnlyList<Accion> Todas => _orden.Select(id => _acciones[id]).ToList();

    public bool Existe(string id) => _acciones.ContainsKey(id);

    public Accion? Buscar(string id) => _acciones.GetValueOrDefault(id);

    public async Task<ResultadoAccion> EjecutarAsync(string id, string origen, CancellationToken ct = default)
    {
        if (!_acciones.TryGetValue(id, out var accion))
            return new ResultadoAccion(id, false, "Acción desconocida", TimeSpan.Zero);

        if (accion.Exclusiva) await _cerrojo.WaitAsync(ct);
        var inicio = DateTime.UtcNow;
        try
        {
            Iniciada?.Invoke(accion.Id, origen);
            await accion.Ejecutar(ct);
            var ok = new ResultadoAccion(accion.Id, true, null, DateTime.UtcNow - inicio);
            Terminada?.Invoke(ok, origen);
            return ok;
        }
        catch (Exception ex)
        {
            var error = new ResultadoAccion(accion.Id, false, ex.Message, DateTime.UtcNow - inicio);
            Terminada?.Invoke(error, origen);
            return error;
        }
        finally
        {
            if (accion.Exclusiva) _cerrojo.Release();
        }
    }

    /// <summary>Lanza la acción en segundo plano y vuelve enseguida. False si no existe.</summary>
    public bool Lanzar(string id, string origen)
    {
        if (!Existe(id)) return false;
        _ = Task.Run(() => EjecutarAsync(id, origen));
        return true;
    }
}
