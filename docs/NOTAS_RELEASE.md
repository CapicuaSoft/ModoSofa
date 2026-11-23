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
[`docs/INSTALACION.md`](https://github.com/jbautistamartin/ModoSofa/blob/main/docs/INSTALACION.md).

ModoSofá es gratuito. Si te resulta útil, puedes [invitarme a un café](https://paypal.me/jbautistamartin).
Es completamente opcional.
