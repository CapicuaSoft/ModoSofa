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
