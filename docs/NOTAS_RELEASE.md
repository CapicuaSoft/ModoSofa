**ModoSofá 1.0.0**: primera versión estable, probada en el equipo real con la tele. Enchufas la
tele y el PC pasa a ser una consola; la desenchufas y vuelve a estar como lo dejaste.

> **Si tienes la 0.1.0, instala esta versión a mano una vez** (la 0.1.0 solo sabía avisar). A
> partir de la 1.0.0 el PC se actualiza solo.
>
> **App Android:** cambia de identificador (`com.capicuasoft.modosofa`) y de firma. Si tenías una
> versión anterior en el móvil, desinstálala antes de instalar esta.

## Novedades

- **Pantalla de espera al entrar en modo sofá.** Steam tarda en arrancar; ahora la tele muestra
  «Entrando en modo sofá» con el paso en curso y los segundos, y se cierra sola cuando aparece Big
  Picture. Se desactiva con `"Lanzadores": { "PantallaEspera": false }`.
- **Se actualiza solo.** Una vez al día ModoSofá mira si hay versión nueva; si la hay, descarga el
  instalador, comprueba su SHA-256 y lo instala en silencio, **solo en modo trabajo**. Conserva la
  configuración y las vinculaciones. Para que solo avise: `"Actualizaciones": { "InstalarSolo": false }`.
  La app Android avisa, descarga y verifica el APK, y Android pide confirmar.
- **Tele LG NetCast** (anteriores a webOS): se vincula con el código que muestra la tele (tarjeta
  «Tele» en el móvil) y se apaga al volver a trabajar. Las webOS, además, se encienden y cambian
  de entrada.
- **Espera a la entrada HDMI:** si enchufas el cable y después enciendes la tele, ModoSofá espera
  hasta 90 s a que muestres la entrada del PC.
- **Teams vuelve a la bandeja** al volver a trabajar si estaba ahí, sin abrir su ventana.
- **Mando:** `Back` mantenido 2 segundos abre el menú en la tele, que toma el foco (queda encima de
  ES-DE y el juego no recibe las pulsaciones). **«Cerrar juego»** cierra lo que tienes delante:
  RetroArch, ES-DE o un juego de Steam (este, con confirmación).
- **«Acerca de»** en la bandeja, en *Ajustes* del móvil y en la app, con **«Invitar a un café»**.
- **Escanear QR** en la app Android para vincularla.
- Los datos pasan a `%APPDATA%\CapicuaSoft\ModoSofa`; se mueven solos al arrancar.

## Corregido

- Al volver a trabajar ya no queda un monitor apagado, aunque la entrada fallara a medias.
- El sonido pasa a la tele aunque Windows tarde en crear su salida HDMI.
- Con Steam abierto, el menú ya no se abre y se cierra a la vez (Steam Input duplica el mando).

**Si actualizas desde la 0.1.0** con un `config.json` ya creado, el mando sigue con
`Back + Start`: en la sección `Mando` pon `"ComboOverlay": "Back"`, `"MantenerMs": 2000` y
`"Atajos": {}` (o borra la sección para usar los valores nuevos).

## Descargas

| Archivo | Qué es | Dónde va |
|---|---|---|
| `ModoSofa-Setup-1.0.0.exe` | **Instalador** (recomendado) | El PC |
| `ModoSofa-1.0.0-win-x64-portable.zip` | La misma aplicación sin instalador | El PC |
| `modosofa-1.0.0.apk` | App Android firmada (panel + encender el PC) | El móvil |
| `lambda-modosofa-1.0.0.zip` | Función de AWS Lambda (Python 3.13) | AWS |
| `modelo-alexa-es-ES-1.0.0.json` | Modelo de interacción del skill | Alexa Developer Console |
| `SHA256SUMS.txt` | Sumas de comprobación (las usa la autoactualización) | — |

La instalación paso a paso está en
[`docs/INSTALACION.md`](https://github.com/CapicuaSoft/ModoSofa/blob/main/docs/INSTALACION.md).

ModoSofá es gratuito. Si te resulta útil, puedes [invitarme a un café](https://paypal.me/jbautistamartin).
Es completamente opcional.
