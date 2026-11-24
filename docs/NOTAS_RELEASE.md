**ModoSofá 1.0.1**: OpenBOR se trata como un emulador de ES-DE, igual que RetroArch.

> Si tienes la 1.0.0 en el PC, **se actualiza sola** en modo trabajo (una vez al día, o al
> arrancar). Si vienes de la 0.1.0, instala esta versión a mano una vez.

## Cambiado

- **OpenBOR (y otros emuladores de ES-DE):** ES-DE lanza OpenBOR a través de `LanzarOpenBor.exe`,
  y antes, por estar bajo `C:\Games`, contaba como juego de Steam: «Cerrar juego» pedía
  confirmación y volvía a Steam. Ahora no se minimiza al entrar en modo sofá, «Cerrar juego» lo
  cierra sin preguntar y vuelve a ES-DE. Se pueden añadir otros emuladores en
  `"Lanzadores": { "EmuladoresEsDe": [...] }` (por defecto `OpenBOR` y `LanzarOpenBor`).

## Descargas

| Archivo | Qué es | Dónde va |
|---|---|---|
| `ModoSofa-Setup-1.0.1.exe` | **Instalador** (recomendado) | El PC |
| `ModoSofa-1.0.1-win-x64-portable.zip` | La misma aplicación sin instalador | El PC |
| `modosofa-1.0.1.apk` | App Android firmada (panel + encender el PC) | El móvil |
| `lambda-modosofa-1.0.1.zip` | Función de AWS Lambda (Python 3.13) | AWS |
| `modelo-alexa-es-ES-1.0.1.json` | Modelo de interacción del skill | Alexa Developer Console |
| `SHA256SUMS.txt` | Sumas de comprobación (las usa la autoactualización) | — |

La instalación paso a paso está en
[`docs/INSTALACION.md`](https://github.com/CapicuaSoft/ModoSofa/blob/main/docs/INSTALACION.md).

ModoSofá es gratuito. Si te resulta útil, puedes [invitarme a un café](https://paypal.me/jbautistamartin).
Es completamente opcional.
