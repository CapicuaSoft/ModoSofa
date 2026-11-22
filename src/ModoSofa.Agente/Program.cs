// ModoSofá — convierte el PC de trabajo en una consola de salón y vuelve a dejarlo como estaba.
using Microsoft.AspNetCore.StaticFiles;
using ModoSofa.Agente.Api;
using ModoSofa.Agente.Efectos;
using ModoSofa.Agente.Infraestructura;
using ModoSofa.Agente.Interfaz;
using ModoSofa.Agente.Servicios;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

// Una sola instancia. "--esperar" lo usa el reinicio tras guardar la configuración.
using var mutex = new Mutex(false, @"Local\ModoSofa.Agente");
if (!mutex.WaitOne(args.Contains("--esperar") ? TimeSpan.FromSeconds(20) : TimeSpan.Zero))
{
    MessageBox.Show("ModoSofá ya está en marcha (icono en la bandeja del sistema).", "ModoSofá",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

Directory.CreateDirectory(Rutas.Datos);
Configuracion config;
try
{
    config = Configuracion.Cargar(Rutas.Config);
}
catch (Exception ex)
{
    MessageBox.Show($"La configuración no es válida:\n{Rutas.Config}\n\n{ex.Message}", "ModoSofá",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
}

var registroLog = new RegistroLog(Rutas.Logs);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
});

builder.Logging.ClearProviders();
builder.Logging.AddProvider(registroLog);
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

builder.WebHost.UseUrls($"http://{(config.EscucharEnRed ? "0.0.0.0" : "127.0.0.1")}:{config.Puerto}");

var s = builder.Services;
s.AddSingleton(config);
s.AddSingleton(registroLog);
s.AddSingleton<AlmacenEstado>();
s.AddSingleton<AlmacenSecretos>();
s.AddSingleton<Eventos>();
s.AddSingleton<RegistroAcciones>();

s.AddSingleton<Pantallas>();
s.AddSingleton<Audio>();
s.AddSingleton<Ventanas>();
s.AddSingleton<Energia>();
s.AddSingleton<RetroArch>();
s.AddSingleton<Lanzadores>();
s.AddSingleton<Procesos>();
s.AddSingleton<TeleLg>();
s.AddSingleton<ModoServicio>();

// Servicios en segundo plano que además consulta la API: una sola instancia de cada uno.
s.AddSingleton<Bandeja>();
s.AddSingleton<IOverlay>(sp => sp.GetRequiredService<Bandeja>());
s.AddHostedService(sp => sp.GetRequiredService<Bandeja>());
s.AddSingleton<VigilantePantallas>();
s.AddHostedService(sp => sp.GetRequiredService<VigilantePantallas>());
s.AddSingleton<MandoServicio>();
s.AddHostedService(sp => sp.GetRequiredService<MandoServicio>());
s.AddSingleton<PanelEstado>();
s.AddHostedService(sp => sp.GetRequiredService<PanelEstado>());
s.AddHostedService<GuardianFoco>();

s.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

var app = builder.Build();

registroLog.Nueva += e => app.Services.GetRequiredService<Eventos>().Publicar("log", e);
CatalogoAcciones.Registrar(app.Services);

var tipos = new FileExtensionContentTypeProvider();
tipos.Mappings[".webmanifest"] = "application/manifest+json";
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = tipos,
    OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
});
app.UsarSeguridad();
app.MapearApi();

var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModoSofa");
app.Lifetime.ApplicationStarted.Register(() =>
{
    log.LogInformation("ModoSofá {Version} en marcha en el puerto {Puerto}.", PanelEstado.Version, config.Puerto);
    _ = app.Services.GetRequiredService<ModoServicio>().RecuperarAlArrancarAsync();
});

try
{
    app.Run();
}
catch (IOException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
{
    MessageBox.Show($"El puerto {config.Puerto} está ocupado (¿AlexaController sigue en marcha?).\nCámbialo en {Rutas.Config}.",
        "ModoSofá", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
finally
{
    app.Services.GetRequiredService<Energia>().PermitirSuspension();
}
