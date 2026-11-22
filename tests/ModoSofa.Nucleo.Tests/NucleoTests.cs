using ModoSofa.Nucleo;

namespace ModoSofa.Nucleo.Tests;

public class CombosTests
{
    [Fact]
    public void Parsea_combinaciones_sin_distinguir_mayusculas()
    {
        Assert.Equal(Botones.Back | Botones.Start | Botones.B, Combos.Parsear("back + START+b"));
    }

    [Fact]
    public void Rechaza_botones_desconocidos()
    {
        Assert.Throws<FormatException>(() => Combos.Parsear("Back+Guide"));
    }
}

public class DetectorCombosTests
{
    private static DetectorCombos Nuevo() => DetectorCombos.DesdeConfig(new MandoConfig { MantenerMs = 800 });

    private const Botones BackStart = Botones.Back | Botones.Start;

    [Fact]
    public void Back_Start_mantenido_abre_el_overlay_una_sola_vez()
    {
        var d = Nuevo();
        Assert.DoesNotContain(d.Procesar(BackStart, 0), e => e is AlternarOverlay);
        Assert.DoesNotContain(d.Procesar(BackStart, 500), e => e is AlternarOverlay);
        Assert.Contains(d.Procesar(BackStart, 800), e => e is AlternarOverlay);
        Assert.DoesNotContain(d.Procesar(BackStart, 2000), e => e is AlternarOverlay);
    }

    [Fact]
    public void Soltar_antes_de_tiempo_no_abre_el_overlay()
    {
        var d = Nuevo();
        d.Procesar(BackStart, 0);
        d.Procesar(Botones.Ninguno, 400);
        d.Procesar(BackStart, 500);
        Assert.DoesNotContain(d.Procesar(BackStart, 1000), e => e is AlternarOverlay);
        Assert.Contains(d.Procesar(BackStart, 1300), e => e is AlternarOverlay);
    }

    [Fact]
    public void Atajo_se_dispara_al_completar_la_combinacion_y_bloquea_el_overlay()
    {
        var d = Nuevo();
        d.Procesar(BackStart, 0);
        var eventos = d.Procesar(BackStart | Botones.B, 100);
        Assert.Contains(eventos, e => e is Atajo { AccionId: "juego.cerrar" });

        // Volver a Back+Start sin soltar todo no abre el overlay aunque pase el tiempo.
        d.Procesar(BackStart, 200);
        Assert.DoesNotContain(d.Procesar(BackStart, 5000), e => e is AlternarOverlay);

        // Tras soltar, todo vuelve a funcionar.
        d.Procesar(Botones.Ninguno, 5100);
        d.Procesar(BackStart, 5200);
        Assert.Contains(d.Procesar(BackStart, 6000), e => e is AlternarOverlay);
    }

    [Fact]
    public void El_menu_de_RetroArch_no_dispara_nada()
    {
        var d = Nuevo();
        var menuRetroArch = BackStart | Botones.LB | Botones.RB;
        d.Procesar(menuRetroArch, 0);
        var eventos = d.Procesar(menuRetroArch, 3000);
        Assert.DoesNotContain(eventos, e => e is AlternarOverlay or Atajo);
    }

    [Fact]
    public void Informa_de_cada_boton_nuevo()
    {
        var d = Nuevo();
        d.Procesar(Botones.A, 0);
        var eventos = d.Procesar(Botones.A | Botones.DPadDown, 10);
        Assert.Equal([new Pulsado(Botones.DPadDown)], eventos);
    }
}

public class DetectorTeleTests
{
    [Fact]
    public void Solo_informa_cuando_el_cambio_se_mantiene()
    {
        var d = new DetectorTele(4000);
        Assert.Null(d.Procesar(true, 0));
        Assert.Null(d.Procesar(false, 1000)); // rebote del HDMI
        Assert.Null(d.Procesar(true, 2000));
        Assert.Null(d.Procesar(true, 5000));
        Assert.True(d.Procesar(true, 6000));
        Assert.Null(d.Procesar(true, 7000));
        Assert.Null(d.Procesar(false, 8000));
        Assert.False(d.Procesar(false, 12000));
    }

    [Fact]
    public void Tele_ya_conectada_al_arrancar_se_informa()
    {
        var d = new DetectorTele(0);
        Assert.True(d.Procesar(true, 0));
    }
}

public class PuenteAlexaTests
{
    [Fact]
    public void Accion_directa()
    {
        var (accion, r) = PuenteAlexa.Resolver(new("ActivarModoSofaIntent", null));
        Assert.Equal("modo.sofa", accion);
        Assert.True(r.Terminar);
    }

    [Fact]
    public void Apagar_pide_confirmacion_y_si_la_ejecuta()
    {
        var (accion, r) = PuenteAlexa.Resolver(new("ApagarEquipoIntent", null));
        Assert.Null(accion);
        Assert.Equal("equipo.apagar", r.Pendiente);
        Assert.False(r.Terminar);

        var (confirmada, _) = PuenteAlexa.Resolver(new("AMAZON.YesIntent", r.Pendiente));
        Assert.Equal("equipo.apagar", confirmada);
    }

    [Fact]
    public void Si_sin_nada_pendiente_no_ejecuta_nada()
    {
        Assert.Null(PuenteAlexa.Resolver(new("AMAZON.YesIntent", null)).Accion);
        Assert.Null(PuenteAlexa.Resolver(new("AMAZON.YesIntent", "algo.inventado")).Accion);
    }

    [Fact]
    public void No_cancela()
    {
        var (accion, r) = PuenteAlexa.Resolver(new("AMAZON.NoIntent", "equipo.apagar"));
        Assert.Null(accion);
        Assert.Null(r.Pendiente);
    }
}

public class RegistroAccionesTests
{
    [Fact]
    public async Task Ejecuta_y_captura_errores()
    {
        var registro = new RegistroAcciones();
        registro.Registrar(new Accion("ok", "Ok", "g", _ => Task.CompletedTask));
        registro.Registrar(new Accion("mal", "Mal", "g", _ => throw new InvalidOperationException("fallo")));

        Assert.True((await registro.EjecutarAsync("ok", "test")).Correcta);
        var mal = await registro.EjecutarAsync("mal", "test");
        Assert.False(mal.Correcta);
        Assert.Equal("fallo", mal.Error);
        Assert.False((await registro.EjecutarAsync("nada", "test")).Correcta);
    }

    [Fact]
    public async Task Las_exclusivas_no_se_solapan()
    {
        var registro = new RegistroAcciones();
        var dentro = 0;
        var maximo = 0;
        registro.Registrar(new Accion("lenta", "Lenta", "g", async _ =>
        {
            var n = Interlocked.Increment(ref dentro);
            maximo = Math.Max(maximo, n);
            await Task.Delay(50);
            Interlocked.Decrement(ref dentro);
        }));

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => registro.EjecutarAsync("lenta", "test")));
        Assert.Equal(1, maximo);
    }

    [Fact]
    public void Rechaza_duplicados()
    {
        var registro = new RegistroAcciones();
        registro.Registrar(new Accion("a", "A", "g", _ => Task.CompletedTask));
        Assert.Throws<InvalidOperationException>(() =>
            registro.Registrar(new Accion("A", "A", "g", _ => Task.CompletedTask)));
    }
}

public class ConfiguracionTests
{
    [Fact]
    public void Un_json_parcial_conserva_los_valores_por_defecto()
    {
        var config = Configuracion.Deserializar("""
            {
              // comentarios permitidos
              "Tele": { "NombrePantalla": "LG TV SSCR2" },
            }
            """);
        Assert.Equal("LG TV SSCR2", config.Tele.NombrePantalla);
        Assert.True(config.Tele.EntrarAlConectar);
        Assert.Equal(5790, config.Puerto);
        Assert.Contains("Back+Start+B", config.Mando.Atajos.Keys);
    }

    [Fact]
    public void Ida_y_vuelta()
    {
        var original = new Configuracion { Puerto = 1234 };
        Assert.Equal(1234, Configuracion.Deserializar(original.Serializar()).Puerto);
    }

    [Fact]
    public void Los_atajos_por_defecto_son_validos()
    {
        var d = DetectorCombos.DesdeConfig(new MandoConfig());
        Assert.NotNull(d);
    }
}
