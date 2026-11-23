// ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
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

// ModoSofá — convierte el PC de trabajo en una consola de salón y vuelve a dejarlo como estaba.
using Microsoft.AspNetCore.StaticFiles;
using CapicuaSoft.ModoSofa.Agente.Api;
using CapicuaSoft.ModoSofa.Agente.Efectos;
using CapicuaSoft.ModoSofa.Agente.Infraestructura;
using CapicuaSoft.ModoSofa.Agente.Interfaz;
using CapicuaSoft.ModoSofa.Agente.Servicios;
using CapicuaSoft.ModoSofa.Agente.Win32;
using CapicuaSoft.ModoSofa.Nucleo;

// «--vista-menu <png>»: dibuja el menú de la bandeja en una imagen y sale (revisión de iconos).
if (args.Length == 2 && args[0] == "--vista-menu")
{
    VistaMenu.Guardar(args[1]);
    return;
}

// Una sola instancia. "--esperar" lo usa el reinicio tras guardar la configuración.
using var mutex = new Mutex(false, @"Local\CapicuaSoft.ModoSofa.Agente");
if (!mutex.WaitOne(args.Contains("--esperar") ? TimeSpan.FromSeconds(20) : TimeSpan.Zero))
{
    MessageBox.Show("ModoSofá ya está en marcha (icono en la bandeja del sistema).", "ModoSofá",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

List<string> migradas;
try
{
    migradas = Rutas.Migrar();
}
catch (Exception ex)
{
    MessageBox.Show($"No se pudieron mover los datos a la carpeta de CapicuaSoft:\n{ex.Message}", "ModoSofá",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
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
s.AddSingleton<TeleWebOs>();
s.AddSingleton<TeleNetCast>();
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
s.AddSingleton<ActualizacionServicio>();
s.AddHostedService(sp => sp.GetRequiredService<ActualizacionServicio>());

s.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

var app = builder.Build();

registroLog.Nueva += e => app.Services.GetRequiredService<Eventos>().Publicar("log", e);
CatalogoAcciones.Registrar(app.Services);

var tipos = new FileExtensionContentTypeProvider();
tipos.Mappings[".webmanifest"] = "application/manifest+json";
// Para poder pasar la app Android al móvil por la Wi-Fi de casa (wwwroot/modosofa.apk).
tipos.Mappings[".apk"] = "application/vnd.android.package-archive";
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
    log.LogInformation("ModoSofá {Version} (CapicuaSoft) en marcha en el puerto {Puerto}.", PanelEstado.Version, config.Puerto);
    foreach (var m in migradas) log.LogInformation("Datos movidos a la carpeta de CapicuaSoft: {Migracion}", m);
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
