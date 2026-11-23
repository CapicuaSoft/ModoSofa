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

/// <summary>
/// Anti-rebote de la conexión de la tele. Al enchufar un HDMI, Windows puede anunciar y retirar
/// la pantalla varias veces en pocos segundos; solo se informa de un cambio cuando se mantiene
/// <c>esperaMs</c>. Parte de «desconectada», así que si al arrancar ya está la tele, se informa
/// de la conexión.
/// </summary>
public sealed class DetectorTele(int esperaMs)
{
    private bool _estable;
    private bool _candidato;
    private long _desde;

    public bool Conectada => _estable;

    /// <returns>true = acaba de conectarse, false = acaba de desconectarse, null = sin cambios.</returns>
    public bool? Procesar(bool conectada, long ahoraMs)
    {
        if (conectada != _candidato)
        {
            _candidato = conectada;
            _desde = ahoraMs;
        }

        if (_candidato != _estable && ahoraMs - _desde >= esperaMs)
        {
            _estable = _candidato;
            return _estable;
        }

        return null;
    }
}
