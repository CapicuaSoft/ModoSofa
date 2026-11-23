# Registro de cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versionado según
[SemVer](https://semver.org/lang/es/).

Las notas largas de cada release (artefactos e instalación) están en
[`docs/NOTAS_RELEASE.md`](docs/NOTAS_RELEASE.md), que es lo que `release.ps1` publica en GitHub.

## [No publicado]

## [0.2.0] - 2026-11-22

### Añadido

- **Autoactualización del PC:** si hay un release nuevo, ModoSofá descarga el instalador, comprueba
  su SHA-256 y lo instala en silencio, solo en modo trabajo (si estás jugando, espera a que
  vuelvas). Conserva la configuración y las vinculaciones. Con
  `"Actualizaciones": { "InstalarSolo": false }` solo avisa.
- **Autoactualización de la app Android:** avisa de la versión nueva, descarga el APK, lo
  verifica con SHA-256 y Android pide confirmar la instalación.
- **«Acerca de»** en el PC (menú de la bandeja), en Ajustes del móvil y en la app Android:
  versión, estado de la actualización con «Buscar ahora» y «Actualizar ahora», enlace al
  proyecto, licencia e **«Invitar a un café»**, que también está en el menú de la bandeja.

### Cambiado

- Cabecera de licencia LGPL-2.1 en todos los ficheros de código, y secciones de licencia y
  donaciones en el README.
- El instalador, en una actualización silenciosa, no repite las tareas que piden administrador
  (firewall, RetroArch, inicio de sesión automático).

### Nota

- La 0.1.0 solo **avisa** de la versión nueva: hay que instalar la 0.2.0 a mano una vez. A partir
  de ahí, las actualizaciones son automáticas.

## [0.1.0] - 2026-11-22

Primera versión. Sucede a [AlexaController](https://github.com/jbautistamartin/AlexaController).

### Añadido

- **Modo sofá automático:** al enchufar la tele, pantalla única, audio por la tele, programas de
  trabajo cerrados (y reabiertos al salir), el resto de ventanas minimizadas, sin suspensión y
  Steam en Big Picture. Al desenchufarla, todo vuelve a su sitio. Si el PC se cuelga a mitad, se
  deshace al arrancar.
- **Mando:** `Back + Start` mantenido abre un menú en la tele que no quita el foco al juego;
  `Back + Start + botón` son atajos (cerrar juego, enfocar, volumen).
- **Guardián de foco:** aparta las ventanas de trabajo que se pongan delante del juego.
- **RetroArch:** se cierra y se pausa por sus comandos de red, sin perder partidas.
- **Móvil:** panel web instalable con estado y log en vivo, vinculado por QR; app Android con
  encendido del PC por Wake-on-LAN.
- **Alexa:** Lambda genérica y modelo de interacción en español; apagar, reiniciar y suspender
  piden confirmación. Pensado para Tailscale Funnel en lugar de ngrok.
- **Tele LG webOS:** encender, cambiar de entrada y apagar.
- **Instalador** sin permisos de administrador, con inicio con Windows, regla de firewall,
  activación de RetroArch e inicio de sesión automático opcionales.
- **Aviso de versión nueva** en la bandeja y en el móvil.
