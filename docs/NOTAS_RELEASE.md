**ModoSofá 0.2.0**: los dos programas se actualizan solos y aparece «Acerca de» con la opción de
invitar a un café.

> Si tienes la **0.1.0**, instala esta versión a mano una vez (la 0.1.0 solo sabía avisar).
> A partir de la 0.2.0, las versiones nuevas se instalan solas.

## Novedades

- **El PC se actualiza solo.** Una vez al día ModoSofá mira si hay versión nueva; si la hay,
  descarga el instalador, comprueba su SHA-256 y lo instala en silencio. **Solo en modo trabajo**:
  si estás jugando, espera a que vuelvas. Conserva la configuración y la vinculación del móvil y de
  Alexa. Si prefieres que solo avise: `"Actualizaciones": { "InstalarSolo": false }` en
  `config.json`.
- **La app Android se actualiza** desde la propia app: avisa, descarga y verifica el APK, y Android
  te pide confirmar.
- **«Acerca de»** en el menú de la bandeja, en *Ajustes* del móvil y en la app: versión,
  actualizaciones, proyecto, licencia (LGPL-2.1) e **«Invitar a un café»**.

## Cambios tras las pruebas con la tele

- El menú se abre **manteniendo `Back` 2 segundos** y toma el foco: queda encima de ES-DE y el
  juego no recibe las pulsaciones. Ya no hay atajos directos.
- **«Cerrar juego»** cierra lo que tienes delante: RetroArch, ES-DE o un juego de Steam (este,
  con confirmación).
- Corregido: el sonido no pasaba a la tele, al volver al trabajo quedaba apagado un monitor y, con
  Steam abierto, el menú se abría y se cerraba a la vez.
- **Si actualizas desde la 0.1.0** con un `config.json` ya creado, el mando sigue con
  `Back + Start`: en la sección `Mando` pon `"ComboOverlay": "Back"`, `"MantenerMs": 2000` y
  `"Atajos": {}` (o borra la sección para usar los valores nuevos).

## Descargas

| Archivo | Qué es | Dónde va |
|---|---|---|
| `ModoSofa-Setup-0.2.0.exe` | **Instalador** (recomendado) | El PC |
| `ModoSofa-0.2.0-win-x64-portable.zip` | La misma aplicación sin instalador | El PC |
| `modosofa-0.2.0.apk` | App Android firmada (panel + encender el PC) | El móvil |
| `lambda-modosofa-0.2.0.zip` | Función de AWS Lambda (Python 3.13) | AWS |
| `modelo-alexa-es-ES-0.2.0.json` | Modelo de interacción del skill | Alexa Developer Console |
| `SHA256SUMS.txt` | Sumas de comprobación (las usa la autoactualización) | — |

La instalación paso a paso está en
[`docs/INSTALACION.md`](https://github.com/CapicuaSoft/ModoSofa/blob/main/docs/INSTALACION.md).

ModoSofá es gratuito. Si te resulta útil, puedes [invitarme a un café](https://paypal.me/jbautistamartin).
Es completamente opcional.
