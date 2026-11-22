namespace ModoSofa.Agente.Infraestructura;

public sealed record EntradaLog(DateTimeOffset Hora, string Nivel, string Categoria, string Mensaje, string? Excepcion);

/// <summary>
/// Proveedor de log propio: fichero diario en %LOCALAPPDATA%\ModoSofa\logs y las últimas entradas
/// en memoria para el visor del móvil, que las recibe en vivo.
/// </summary>
public sealed class RegistroLog : ILoggerProvider
{
    private const int Capacidad = 1000;
    private readonly string _carpeta;
    private readonly LinkedList<EntradaLog> _ultimas = new();
    private readonly Lock _cerrojo = new();

    public event Action<EntradaLog>? Nueva;

    public RegistroLog(string carpeta)
    {
        _carpeta = carpeta;
        Directory.CreateDirectory(carpeta);
        Purgar();
    }

    public string FicheroDeHoy => Path.Combine(_carpeta, $"modosofa-{DateTime.Now:yyyyMMdd}.log");

    public IReadOnlyList<EntradaLog> Ultimas(int n)
    {
        lock (_cerrojo) return _ultimas.Skip(Math.Max(0, _ultimas.Count - n)).ToList();
    }

    public void Vaciar()
    {
        lock (_cerrojo) _ultimas.Clear();
    }

    public ILogger CreateLogger(string categoria) => new Registrador(this, categoria);

    public void Dispose() { }

    private void Escribir(EntradaLog e)
    {
        var linea = $"[{e.Hora:HH:mm:ss} {e.Nivel}] {e.Mensaje}{(e.Excepcion is null ? "" : Environment.NewLine + e.Excepcion)}{Environment.NewLine}";
        lock (_cerrojo)
        {
            _ultimas.AddLast(e);
            if (_ultimas.Count > Capacidad) _ultimas.RemoveFirst();
            try { File.AppendAllText(FicheroDeHoy, linea); } catch (IOException) { /* el log nunca tumba la app */ }
        }
        Nueva?.Invoke(e);
    }

    /// <summary>Conserva 14 días de ficheros.</summary>
    private void Purgar()
    {
        foreach (var f in Directory.GetFiles(_carpeta, "modosofa-*.log"))
            if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-14))
                try { File.Delete(f); } catch { /* en uso */ }
    }

    private sealed class Registrador(RegistroLog registro, string categoria) : ILogger
    {
        private readonly string _categoria = categoria.Split('.').Last();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => nivel != LogLevel.None;

        public void Log<TState>(LogLevel nivel, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formato)
        {
            if (!IsEnabled(nivel)) return;
            registro.Escribir(new EntradaLog(DateTimeOffset.Now, Abreviar(nivel), _categoria, formato(state, ex), ex?.ToString()));
        }

        private static string Abreviar(LogLevel n) => n switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => "FTL",
        };
    }
}
