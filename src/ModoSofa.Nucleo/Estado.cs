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

namespace ModoSofa.Nucleo;

public enum Modo
{
    Trabajo,
    Sofa,
}

/// <summary>
/// Lo que ModoSofá ha cambiado y cómo deshacerlo. Se guarda tras cada paso, de modo que un
/// cuelgue o un reinicio a mitad no impiden volver al modo trabajo.
/// </summary>
public sealed class EstadoPersistente
{
    public Modo Modo { get; set; } = Modo.Trabajo;
    public DateTimeOffset? Desde { get; set; }

    /// <summary>Topología de pantallas previa (rutas y modos CCD, serializados).</summary>
    public string? PantallasAnteriores { get; set; }

    public string? AudioAnterior { get; set; }
    public float? VolumenAnterior { get; set; }

    public string? PlanEnergiaAnterior { get; set; }

    public List<VentanaGuardada> Ventanas { get; set; } = [];
    public List<ProcesoCerrado> ProcesosCerrados { get; set; } = [];

    /// <summary>Procesos que tenían ventanas de trabajo al entrar: el guardián de foco los vigila.</summary>
    public List<int> ProcesosTrabajo { get; set; } = [];

    public void Limpiar()
    {
        Modo = Modo.Trabajo;
        Desde = null;
        PantallasAnteriores = null;
        AudioAnterior = null;
        VolumenAnterior = null;
        PlanEnergiaAnterior = null;
        Ventanas.Clear();
        ProcesosCerrados.Clear();
        ProcesosTrabajo.Clear();
    }
}

public sealed record VentanaGuardada(long Hwnd, int ProcesoId, string Proceso, string Titulo, string Posicion);

/// <summary>Un proceso cerrado al entrar. <see cref="Aumid"/> se usa para apps empaquetadas (MSIX).</summary>
public sealed record ProcesoCerrado(string Nombre, string? Ruta, string? Aumid);

/// <summary>Credenciales generadas en el primer arranque. <c>%APPDATA%\ModoSofa\secretos.json</c>.</summary>
public sealed class Secretos
{
    public string TokenApi { get; set; } = "";
    public string TokenAlexa { get; set; } = "";
    public string LgClaveCliente { get; set; } = "";
}
