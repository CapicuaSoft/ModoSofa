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
