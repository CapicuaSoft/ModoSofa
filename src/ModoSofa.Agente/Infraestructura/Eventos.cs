using System.Text.Json;
using System.Threading.Channels;

namespace ModoSofa.Agente.Infraestructura;

/// <summary>Difusión de eventos (estado, log, acciones) a los clientes SSE: móvil y overlay.</summary>
public sealed class Eventos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly List<Channel<string>> _suscriptores = [];
    private readonly Lock _cerrojo = new();

    public (ChannelReader<string> Lector, IDisposable Baja) Suscribir()
    {
        var canal = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_cerrojo) _suscriptores.Add(canal);
        return (canal.Reader, new Baja(() =>
        {
            lock (_cerrojo) _suscriptores.Remove(canal);
            canal.Writer.TryComplete();
        }));
    }

    public void Publicar(string tipo, object datos)
    {
        var mensaje = $"event: {tipo}\ndata: {JsonSerializer.Serialize(datos, Json)}\n\n";
        lock (_cerrojo)
            foreach (var s in _suscriptores) s.Writer.TryWrite(mensaje);
    }

    private sealed class Baja(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }
}
