# Arquitectura

```
┌──────────────────────── PC (sesión del usuario, sin admin) ──────────────────────────┐
│ ModoSofa.exe                                                                         │
│                                                                                      │
│  Disparadores              Núcleo                         Efectos (Win32 / red)      │
│  ───────────               ──────                         ──────────────────────     │
│  VigilantePantallas ─┐                                    Pantallas   (API CCD)      │
│  MandoServicio ──────┤                                    Audio       (Core Audio)   │
│  Overlay (WebView2) ─┼──►  RegistroAcciones ──► ModoServicio ──► Ventanas  (user32)  │
│  API /api/acciones ──┤     (cola exclusiva)                Energia    (powercfg)     │
│  API /api/alexa ─────┤                                     Lanzadores (Steam, ES-DE) │
│  Bandeja ────────────┘                                     RetroArch  (UDP 55355)    │
│                                                            TeleLg     (webOS/NetCast)│
│  GuardianFoco (vigila el foco en modo sofá)                Procesos (cerrar/relanzar)│
│  PanelEstado ──► Eventos (SSE) ──► móvil y overlay                                   │
│  ModoServicio.Progreso ──► VentanaEspera (pantalla de espera al entrar)              │
│  AlmacenEstado ──► %APPDATA%\CapicuaSoft\ModoSofa\estado.json (para deshacer)        │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

## Principios

1. **No destructivo.** El modo sofá minimiza y recuerda; solo cierra lo que está en
   `Ventanas.CerrarProcesos`, y lo relanza al salir (también apps MSIX, por su AUMID).
2. **Todo pasa por el registro de acciones.** Mando, móvil, overlay, bandeja y Alexa llaman a
   `RegistroAcciones`, que serializa las acciones exclusivas: dos órdenes de «entrar» seguidas no
   se pisan.
3. **Deshacer sobrevive a un cuelgue.** Cada paso apunta en `estado.json` lo que va a cambiar
   *antes* de cambiarlo. Al arrancar, si el estado dice «sofá» y la tele no está, se deshace.
4. **Cada paso es independiente.** Si falla el audio, las pantallas y Steam siguen adelante; el
   error queda en el log (visible en el móvil).
5. **Sin elevación.** Nada de lo que se hace la necesita: pantallas, audio, ventanas, energía,
   procesos propios y XInput funcionan como usuario normal.

## Entrar en modo sofá (`ModoServicio.EntrarAsync`)

1. Tele LG (si está configurada): Wake-on-LAN y cambio de entrada (solo webOS).
2. Pantallas: se guarda la topología (rutas y modos CCD, en base64) y se deja solo la tele,
   conservando su resolución y frecuencia y colocándola en el origen para que sea la principal.
   Si la tele está conectada pero todavía no muestra la entrada del PC, Windows no puede activarla
   (`ERROR_BAD_CONFIGURATION`): se reintenta cada 3 s durante `Tele.EsperaImagenS` (90 s).
3. Audio: se guarda la salida y el volumen, y se cambia a la de la tele (`IPolicyConfig`). Con la
   tele conectada se espera hasta 20 s a que Windows cree su salida HDMI; sin ella, no se espera.
4. Programas de la lista: se apunta si tenían alguna ventana visible o estaban solo en la bandeja,
   `CloseMainWindow`, espera y, si siguen vivos, se terminan.
5. Ventanas: se guarda la posición (`WINDOWPLACEMENT`) de cada ventana de usuario y se minimiza sin
   activarla. Los procesos dueños de esas ventanas son los que vigila el guardián.
6. Energía: plan opcional y `SetThreadExecutionState` en un hilo dedicado.
7. Steam Big Picture (o ES-DE), esperando a su ventana para darle el foco.

Durante la entrada, `ModoServicio.Progreso` avisa de cada paso y la bandeja muestra
`VentanaEspera`: pantalla completa en la pantalla principal (se recoloca en la tele cuando cambia
la topología), siempre encima pero **sin tomar el foco** (`WS_EX_NOACTIVATE`), para no quitárselo
a Big Picture. Se cierra al terminar la entrada, con un clic o a los 3 minutos. Lanzar `steam.exe`
tarda ~15 s en este PC; sin ella parecía que el PC no hacía nada.

Salir hace lo contrario, en orden inverso, y además cierra ES-DE y Steam (`steam -shutdown`).
Los programas cerrados se relanzan: por su AUMID si son apps MSIX y por su ruta si no. Si estaban
solo en la bandeja, se arrancan en segundo plano (Teams con `msteams:system-initiated`, lo mismo
que usa su inicio con Windows) o, si no se sabe cómo, se minimiza su ventana cuando aparece.
La topología guardada solo se restaura si siguen conectadas las mismas pantallas; si se desenchufó
la tele y se volvió a enchufar el monitor que se quitó para ella, o si la entrada falló antes de
guardarla, se pide a Windows la topología extendida, que recuerda la disposición de esos monitores.
Así nunca queda un monitor apagado.

## Detección de la tele

`VigilantePantallas` consulta cada segundo las pantallas conectadas (`QueryDisplayConfig` con
`QDC_ALL_PATHS`, que incluye las conectadas pero inactivas) y compara el nombre EDID, al que se
añade el código de fabricante (`[GSM]` en las LG). `DetectorTele` exige que el cambio se mantenga
`EsperaDeteccionMs` para ignorar los rebotes del HDMI.

Dejar la tele como pantalla única no la «desconecta» (sigue conectada aunque el monitor quede
inactivo), así que no hay bucles. Desenchufar el cable sí, y eso es lo que dispara la salida.

## Mando

`MandoServicio` lee las cuatro ranuras XInput a ~60 Hz en un hilo propio (las vacías, una vez por
segundo: consultar una ranura vacía es caro). `DetectorCombos` convierte las lecturas en eventos:

- Un único detector sobre la **unión** de los botones de todas las ranuras: Steam Input crea un
  mando virtual que repite los del físico, y con un detector por ranura el menú se abría y se
  cerraba en el mismo instante.
- Las combinaciones deben ser **exactas** (Back + Abajo no abre el menú), y tras disparar una no
  se dispara nada hasta soltar todo.
- Las pulsaciones nuevas se reenvían al overlay cuando está abierto.

## Overlay

Ventana WinForms con WebView2, `WS_EX_TOOLWINDOW | TOPMOST`, que **toma el foco** al abrirse y
se lo devuelve a la ventana anterior al cerrarse. Así queda por encima de ES-DE (que también es
«siempre encima») y el juego deja de atender al mando, porque ES-DE, RetroArch y la mayoría de
juegos ignoran el mando sin foco. La primera versión no tomaba el foco (`WS_EX_NOACTIVATE`) y en
las pruebas el juego seguía recibiendo las pulsaciones y ES-DE tapaba el menú. El mando llega a la
página como `PostWebMessageAsString("boton:A")`. Al abrirlo se pausa RetroArch (si estaba jugando)
y al cerrarlo se reanuda. «Cerrar juego» actúa sobre la ventana que tenía el foco antes del menú
(`IOverlay.VentanaDelante`).

Limitación: los juegos en pantalla completa **exclusiva** no dejan dibujar encima. RetroArch y la
mayoría de juegos modernos usan ventana sin bordes, que sí lo permite.

## Tele LG

`TeleLg` elige el protocolo según `Tele.LgTipo` (`Auto` pregunta a la tele):

- **webOS** (`TeleWebOs`, 2014 en adelante): SSAP por WebSocket seguro. Encender por
  Wake-on-LAN, cambiar de entrada y apagar. La primera vez la tele pide permiso y la clave se
  guarda en `secretos.json`.
- **NetCast** (`TeleNetCast`, anteriores, p. ej. la 50LB5830): UDAP por HTTP en el puerto 8080.
  Se vincula con el código que muestra la tele (tarjeta «Tele» del móvil) y solo se puede apagar:
  estas teles no se encienden por red. Se apaga ~8 s después de la orden.

## Guardián de foco

Cada 750 ms, en modo sofá: si la ventana activa pertenece a un proceso que tenía ventanas de
trabajo al entrar, se minimiza y se devuelve el foco al juego. Los procesos que arrancan después
(juegos, lanzadores) nunca se tocan, así que un juego desconocido no puede ser apartado por error.

## API

| Ruta | Método | Token | |
|---|---|---|---|
| `/api/estado` | GET | móvil | Instantánea del estado |
| `/api/eventos` | GET | móvil (`?t=`) | SSE: `estado`, `log`, `accion` |
| `/api/acciones` | GET | móvil | Catálogo de acciones |
| `/api/acciones/{id}` | POST | móvil | Ejecuta en segundo plano (202) |
| `/api/log` | GET / DELETE | móvil | Últimas entradas del log / vaciar |
| `/api/config` | GET / PUT | móvil | Configuración (PUT reinicia el agente) |
| `/api/diagnostico` | GET | móvil | Pantallas, audio, red, Steam, ES-DE, RetroArch |
| `/api/alexa` | POST | Alexa | `{intent, pendiente}` → `{habla, terminar, pendiente}` |

Los tokens están en `%APPDATA%\CapicuaSoft\ModoSofa\secretos.json` y se comparan en tiempo constante. El del
móvil viaja en el fragmento del QR (`#t=`), que nunca llega a los logs de un servidor.

## Alexa

La Lambda solo reenvía el nombre del intent. `PuenteAlexa` (en el núcleo, con tests) decide la
acción, lo que se dice y si hay que confirmar; la acción pendiente viaja en los atributos de
sesión de Alexa. Añadir una orden = una línea en `PuenteAlexa` + sus frases en el modelo.

## Ficheros

| Ruta | Contenido |
|---|---|
| `%APPDATA%\CapicuaSoft\ModoSofa\config.json` | Configuración |
| `%APPDATA%\CapicuaSoft\ModoSofa\estado.json` | Modo actual y lo necesario para deshacer |
| `%APPDATA%\CapicuaSoft\ModoSofa\secretos.json` | Tokens, clave y código de la tele |
| `%LOCALAPPDATA%\CapicuaSoft\ModoSofa\logs\` | Un fichero por día, 14 días |
| `%LOCALAPPDATA%\CapicuaSoft\ModoSofa\WebView2\` | Datos del navegador del overlay |
| `%LOCALAPPDATA%\CapicuaSoft\ModoSofa\instalacion.log` | Lo que hizo cada paso del instalador |
| `%LOCALAPPDATA%\Programs\ModoSofa\` | El programa instalado |

Hasta la 0.2.0 los datos estaban en `%APPDATA%\ModoSofa` y `%LOCALAPPDATA%\ModoSofa`; al arrancar
se mueven solos a las carpetas nuevas.
