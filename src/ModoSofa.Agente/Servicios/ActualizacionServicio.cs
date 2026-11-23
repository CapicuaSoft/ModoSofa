using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Servicios;

public sealed record VersionNueva(string Version, string Url);

/// <summary>
/// Pregunta a GitHub por el último release 30 s después de arrancar y luego cada 24 h. Solo avisa
/// (log, panel del móvil y bandeja): actualizar es ejecutar el instalador nuevo. Reemplazarse a sí
/// mismo en pleno modo sofá dejaría el PC a medio configurar.
/// </summary>
public sealed class ActualizacionServicio(
    Configuracion config, Bandeja bandeja, ILogger<ActualizacionServicio> log) : BackgroundService
{
    private static readonly HttpClient Http = CrearCliente();

    public VersionNueva? Disponible { get; private set; }

    public event Action? Cambio;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.Actualizaciones.Comprobar) return;
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            await ComprobarAsync(ct);
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }

    private async Task ComprobarAsync(CancellationToken ct)
    {
        try
        {
            using var r = await Http.GetAsync($"https://api.github.com/repos/{Versiones.Repositorio}/releases/latest", ct);
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return; // aún no hay releases estables
            r.EnsureSuccessStatusCode();
            var release = await r.Content.ReadFromJsonAsync<ReleaseGitHub>(ct);
            if (release is null || !Versiones.EsMasNueva(release.Etiqueta, PanelEstado.Version)) return;

            var nueva = new VersionNueva(release.Etiqueta.TrimStart('v'), release.Url);
            if (nueva == Disponible) return;
            Disponible = nueva;
            log.LogWarning("Hay una versión nueva de ModoSofá: {Nueva} (tienes la {Actual}). Descárgala en {Url}",
                nueva.Version, PanelEstado.Version, nueva.Url);
            bandeja.Notificar($"ModoSofá {nueva.Version} disponible", "Pulsa para abrir la página de descarga.", nueva.Url);
            Cambio?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            log.LogDebug(ex, "No se pudo comprobar si hay versión nueva.");
        }
    }

    private static HttpClient CrearCliente()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"ModoSofa/{PanelEstado.Version}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    private sealed record ReleaseGitHub(
        [property: JsonPropertyName("tag_name")] string Etiqueta,
        [property: JsonPropertyName("html_url")] string Url);
}
