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

using ModoSofa.Nucleo;

namespace ModoSofa.Nucleo.Tests;

public class VersionesTests
{
    [Theory]
    [InlineData("v1.10.0", "1.9.0", true)]
    [InlineData("1.9.0", "1.10.0", false)]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("v0.1.0", "0.1.0", false)]
    [InlineData("1.2", "1.2.0", false)]
    [InlineData("1.2.1", "1.2", true)]
    [InlineData("v1.0.0-beta", "0.9.0", true)]
    [InlineData("0.1.0", "0.1.0+abcdef", false)]
    public void Compara_como_numeros(string publicada, string actual, bool esperado)
    {
        Assert.Equal(esperado, Versiones.EsMasNueva(publicada, actual));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void Lo_que_no_es_version_nunca_es_mas_nuevo(string? publicada)
    {
        Assert.False(Versiones.EsMasNueva(publicada, "0.1.0"));
    }
}

public class SumasSha256Tests
{
    private const string Hash1 = "6b1c3f4f0d33a7c6e2f8b9d0a1e2c3b4d5e6f708192a3b4c5d6e7f8091a2b3c4";
    private const string Hash2 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Encuentra_el_hash_con_LF_y_CRLF()
    {
        var sumas = $"{Hash1}  ModoSofa-Setup-0.2.0.exe\r\n{Hash2} *modosofa-0.2.0.apk\n";
        Assert.Equal(Hash1, SumasSha256.Buscar(sumas, "ModoSofa-Setup-0.2.0.exe"));
        Assert.Equal(Hash2.ToLowerInvariant(), SumasSha256.Buscar(sumas, "modosofa-0.2.0.apk"));
    }

    [Fact]
    public void Null_si_no_esta_o_la_linea_no_es_valida()
    {
        Assert.Null(SumasSha256.Buscar($"{Hash1}  otro.exe\n", "ModoSofa-Setup-0.2.0.exe"));
        Assert.Null(SumasSha256.Buscar("abc  ModoSofa-Setup-0.2.0.exe\n", "ModoSofa-Setup-0.2.0.exe"));
        Assert.Null(SumasSha256.Buscar("", "x"));
    }
}
