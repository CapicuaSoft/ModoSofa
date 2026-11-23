# Instalación y puesta en marcha

## 1. El agente en el PC

**Con el instalador** (lo normal): descarga `ModoSofa-Setup-X.Y.Z.exe` de la
[página de releases](https://github.com/CapicuaSoft/ModoSofa/releases/latest) y ejecútalo.
Se instala para tu usuario en `%LOCALAPPDATA%\Programs\ModoSofa`, sin permisos de administrador.
Opciones: iniciar con Windows, regla de firewall para el móvil (pide administrador una vez y
además bloquea la entrada a RetroArch desde la red), comandos de red de RetroArch e inicio de
sesión automático. Lo que hace cada paso queda en
`%LOCALAPPDATA%\CapicuaSoft\ModoSofa\instalacion.log`.
Se desinstala desde *Configuración → Aplicaciones*; al final pregunta si borrar la configuración.

Después **se actualiza solo**: una vez al día busca un release nuevo y, si lo hay, lo instala en
silencio en modo trabajo, con la misma configuración. La 0.1.0 solo sabía avisar: desde ella hay
que instalar la versión nueva a mano una vez.

**Desde el repositorio** (desarrollo):

1. `./tools/publicar.ps1` genera `dist/` (agente autocontenido, Lambda, modelo de Alexa y APK).
2. `./tools/instalar.ps1` lo copia a `%LOCALAPPDATA%\Programs\ModoSofa`, lo registra para arrancar
   con Windows y lo inicia. Sirve también para actualizar: para la instancia en marcha y la
   sustituye (si estaba en modo sofá, la nueva versión lo recupera al arrancar).
   Aparece un sofá en la bandeja del sistema.
3. Para que el móvil llegue por la Wi-Fi de casa: ejecuta `tools/permitir-firewall.ps1` (pide
   permisos de administrador una sola vez; abre el puerto 5790 solo en redes privadas).

> AlexaController usa el puerto 5780 y ModoSofá el 5790, así que pueden convivir. Si vienes de
> AlexaController, quita su tarea programada («Iniciar AlexaController») y su túnel ngrok cuando
> ModoSofá funcione.

## 2. Ajustar la configuración

La configuración está en `%APPDATA%\CapicuaSoft\ModoSofa\config.json` (bandeja → *Abrir carpeta de
configuración*, o desde el móvil en **Ajustes**, que reinicia el agente al guardar).

Con la tele enchufada, abre **Ajustes → Diagnóstico** en el móvil y copia los nombres:

| Clave | Qué poner |
|---|---|
| `Tele.NombrePantalla` | Un fragmento del nombre de la tele en «Pantallas conectadas» (por defecto `LG TV`). Debe **no** coincidir con tus monitores |
| `Audio.DispositivoSofa` | Un fragmento de la salida de audio de la tele (por defecto `LG TV`) |
| `Ventanas.CerrarProcesos` | Programas de trabajo que se cierran al entrar (nombre del proceso sin `.exe`) |
| `Energia.PlanSofa` | GUID de un plan de energía (`powercfg /list`), o vacío para no tocarlo |
| `Audio.VolumenInicialSofa` | 0-100, o -1 para no tocar el volumen |
| `Tele.EsperaImagenS` | Segundos que se espera a que la tele muestre la entrada HDMI del PC (90) |
| `Lanzadores.AbrirEsdeAlEntrar` | `true` para entrar en ES-DE en vez de Steam Big Picture |
| `Lanzadores.PantallaEspera` | `false` para no mostrar la pantalla de espera al entrar |

Por defecto entra en modo sofá al detectar la tele y vuelve al trabajo al desenchufarla
(`Tele.EntrarAlConectar` y `Tele.SalirAlDesconectar`). Comprueba en el diagnóstico que
`Tele.NombrePantalla` solo coincide con la tele y no con tus monitores.

Los programas de `Ventanas.CerrarProcesos` se relanzan al volver (`Ventanas.RelanzarAlSalir`). Si
alguno estaba solo en el área de notificaciones, se relanza sin abrir su ventana.

## 3. El móvil

1. Bandeja → **Vincular móvil…** y escanea el QR con la cámara (misma Wi-Fi).
2. En Chrome: menú → **Añadir a pantalla de inicio**.

Por HTTP en la red local Chrome crea un acceso directo; para que se instale como app completa
(sin barra del navegador y con caché sin conexión) hace falta HTTPS, que da Tailscale (sección 4).

### App Android (opcional, para encender el PC)

La PWA no puede encender el PC apagado. La app de `android/` es la misma web dentro de un
envoltorio que sí envía el paquete Wake-on-LAN.

1. Compila: `cd android; ./gradlew assembleRelease` → `app/build/outputs/apk/release/app-release.apk`
   (`tools/publicar.ps1` lo copia a `dist/modosofa.apk`). Necesita el SDK en
   `%LOCALAPPDATA%\Android\Sdk` y `JAVA_HOME` apuntando al JBR de Android Studio. Se firma con
   `C:\Proyectos\Claves\CapicuaSoft.jks` si `android/local.properties` trae `KEYSTORE_PATH`,
   `KEYSTORE_PASSWORD`, `KEY_ALIAS` y `KEY_PASSWORD`. Lo normal es descargar el APK del release.
   La app es `com.capicuasoft.modosofa`: si tenías una versión anterior a la 1.0.0 (otro
   identificador y otra firma), desinstálala antes.
2. En la app, **Ajustes** → **Escanear QR** (el de bandeja → *Vincular móvil…*) o pega el enlace
   (*Copiar enlace*).
3. La MAC del PC se guarda sola la primera vez que conecta.
4. En el PC: activa *Wake on LAN* en la BIOS y, en el Administrador de dispositivos → adaptador
   Ethernet → *Administración de energía*, «Permitir que este dispositivo reactive el equipo».
   Wake-on-LAN funciona por cable; por Wi-Fi casi nunca.

## 4. Tailscale (acceso fuera de casa y HTTPS)

1. Instala Tailscale en el PC y en el móvil con la misma cuenta.
2. En el PC, en una consola:
   ```
   tailscale serve --bg 5790
   ```
   El panel queda en `https://<nombre-del-pc>.<tu-tailnet>.ts.net/`, solo accesible desde tus
   dispositivos. Abre esa dirección en el móvil con `#t=<token>` al final (el token está en el
   enlace del QR) e instálala como app.

## 5. Alexa

Alexa necesita llegar al PC desde internet. Se expone **solo** `/api/alexa`, por otro puerto, con
su propio token:

1. Activa Funnel en tu tailnet (consola de Tailscale → *Access controls* → atributo `funnel`).
2. En el PC:
   ```
   tailscale funnel --bg --https=8443 --set-path /alexa http://127.0.0.1:5790/api/alexa
   ```
   Comprueba con `tailscale funnel status` que la URL pública es
   `https://<nombre-del-pc>.<tu-tailnet>.ts.net:8443/alexa`.
3. **AWS Lambda** (Python 3.13): sube `dist/lambda-modosofa.zip` (o pega
   `alexa/lambda/lambda_function.py`) con estas variables de entorno:
   - `MODOSOFA_URL` = la URL pública del paso anterior
   - `MODOSOFA_TOKEN` = bandeja → **Copiar token de Alexa**
   - Tiempo de espera de la función: 8 s.
4. **Alexa Developer Console**: crea un skill personalizado en español, pega
   `alexa/modelo/es-ES.json` en el *JSON Editor*, compila el modelo y pon como endpoint el ARN de
   la Lambda.
5. Prueba: «Alexa, abre modo sofá» o «Alexa, dile a modo sofá que cierre el juego».

Apagar, reiniciar y suspender piden confirmación («¿Seguro…?» → «sí»).

## 6. La tele LG

ModoSofá sabe controlar dos familias de teles LG (`Tele.LgTipo`: `Auto`, `WebOs` o `NetCast`):

| Tipo | Teles | Qué hace |
|---|---|---|
| `WebOs` | 2014 en adelante | Encender (Wake-on-LAN), cambiar a la entrada HDMI y apagar |
| `NetCast` | Anteriores, p. ej. la 50LB5830 | Solo apagar: no se pueden encender por red |

### webOS

1. En la tele: *Configuración → General → Dispositivos → Ajustes TV* → **Encender con
   Wi-Fi/LAN** (o «LG Connect Apps», según el modelo).
2. Reserva una IP fija para la tele en el router y apunta su MAC.
3. En `config.json`:
   ```json
   "Tele": {
     "ControlarLg": true,
     "LgIp": "192.168.1.50",
     "LgMac": "AA:BB:CC:DD:EE:FF",
     "LgEntrada": "HDMI_1"
   }
   ```
4. La primera orden (p. ej. **Encender tele** desde el móvil) hace que la tele pida permiso:
   acéptalo con su mando. La clave se guarda y no vuelve a preguntar.

### NetCast

1. Reserva una IP fija para la tele en el router.
2. En `config.json`:
   ```json
   "Tele": {
     "ControlarLg": true,
     "LgTipo": "NetCast",
     "LgIp": "192.168.1.50",
     "EncenderAlEntrar": false,
     "ApagarAlSalir": true
   }
   ```
3. Con la tele encendida, en el móvil → tarjeta **Tele** → **Mostrar código en la tele**, escribe
   el código que aparece y pulsa **Vincular**. Se guarda en `secretos.json`.

Al volver a trabajar la tele se apaga unos 8 s después de la orden.

## 7. RetroArch

**Combinaciones recomendadas** (en RetroArch: *Ajustes → Entrada → Teclas rápidas*):
*Combinación para mostrar el menú* = **Mantener Start (2 segundos)** y *Combinación para salir* =
**Ninguna**. Las de serie usan Select (= Back), que es el botón del menú de ModoSofá; para salir
de un juego se usa «Cerrar juego» de ModoSofá, que guarda la partida.

ModoSofá cierra y pausa RetroArch con sus comandos de red, para que se guarden la SRAM y el
estado automático. Ya está activado en
`C:\Games\EmulationStation\Emulators\RetroArch-Win64\retroarch.cfg`
(`network_cmd_enable = "true"`, puerto 55355); la copia original es
`retroarch.cfg.antes-de-modosofa.bak`.

Ojo: RetroArch reescribe su configuración al salir (`config_save_on_exit`). Si lo cambias desde
su menú, comprueba que siga activado en *Ajustes → Red → Comandos de red*.
