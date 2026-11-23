Primera versión de **ModoSofá 0.1.0**: convierte el PC de trabajo en una consola de salón al
enchufar la tele y lo deja como estaba al desenchufarla. Sucede a
[AlexaController](https://github.com/jbautistamartin/AlexaController).

> Versión temprana. Se ha probado el ciclo completo en un PC real (detección, pantallas, audio,
> cierre y reapertura de programas, ventanas, Steam Big Picture, guardián de foco, RetroArch y
> recuperación tras un cuelgue), pero **todavía no con una tele LG real, ni el skill de Alexa
> publicado**. Los problemas, en *Issues*.

## Descargas

| Archivo | Qué es | Dónde va |
|---|---|---|
| `ModoSofa-Setup-0.1.0.exe` | **Instalador** (recomendado) | El PC |
| `ModoSofa-0.1.0-win-x64-portable.zip` | La misma aplicación sin instalador | El PC |
| `modosofa-0.1.0.apk` | App Android firmada (panel + encender el PC) | El móvil |
| `lambda-modosofa-0.1.0.zip` | Función de AWS Lambda (Python 3.13) | AWS |
| `modelo-alexa-es-ES-0.1.0.json` | Modelo de interacción del skill | Alexa Developer Console |
| `SHA256SUMS.txt` | Sumas de comprobación | — |

No hace falta instalar .NET: la aplicación va autocontenida. Requiere Windows 10 1809 o posterior
(64 bits).

## Instalación

1. **PC:** ejecuta `ModoSofa-Setup-0.1.0.exe`. No pide permisos de administrador salvo para la
   regla de firewall, si la marcas. Opciones:
   - *Iniciar con Windows* (marcada).
   - *Permitir el móvil por la Wi-Fi de casa*: regla de firewall solo para redes privadas.
   - *Activar los comandos de red de RetroArch*: con copia de `retroarch.cfg`.
   - *Inicio de sesión automático* (desmarcada): para usarlo nada más encender el PC desde el
     móvil. Usa Autologon de Microsoft; quien encienda el PC entra en tu sesión.
2. **Móvil:** icono de la bandeja → *Vincular móvil…* y escanea el QR. Para encender el PC desde
   el móvil, instala `modosofa-0.1.0.apk` y activa Wake-on-LAN en la BIOS.
3. **Ajustes:** con la tele conectada, en el móvil → *Ajustes → Diagnóstico*, comprueba el nombre
   de la tele y del audio, y ajusta los programas que se cierran al jugar.
4. **Alexa (opcional):** Tailscale Funnel + Lambda + skill, paso a paso en
   [`docs/INSTALACION.md`](https://github.com/jbautistamartin/ModoSofa/blob/main/docs/INSTALACION.md).

**Actualizar** es ejecutar el instalador nuevo: conserva la configuración y la vinculación del
móvil y de Alexa.

## Atajos del mando

| Combinación | Acción |
|---|---|
| `Back + Start` (mantener) | Menú en la tele |
| `Back + Start + B` | Cerrar el juego |
| `Back + Start + Y` | Enfocar el juego |
| `Back + Start + X` | Silenciar |
| `Back + Start + ↑ / ↓` | Volumen |
