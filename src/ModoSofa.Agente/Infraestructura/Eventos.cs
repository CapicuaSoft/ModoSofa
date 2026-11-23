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
