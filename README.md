# ModoSofá

[![Última versión](https://img.shields.io/github/v/release/jbautistamartin/ModoSofa?label=descargar&color=2E7D32)](https://github.com/jbautistamartin/ModoSofa/releases/latest)
[![Licencia](https://img.shields.io/badge/licencia-LGPL--2.1-blue)](LICENSE)
[![Donar con PayPal](https://img.shields.io/badge/Donar-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/jbautistamartin)

Convierte el PC de trabajo en una consola de salón y lo deja después exactamente como estaba.

Enchufas la tele y ModoSofá se encarga del resto:

- deja la tele como **pantalla única** y saca el **sonido por la tele**;
- cierra los programas de trabajo de una lista (Teams, Outlook…) y **minimiza** el resto;
- impide que el PC se suspenda y abre **Steam Big Picture**;
- mientras juegas, aparta cualquier ventana de trabajo que se ponga delante.

Al desenchufar la tele, todo vuelve a su sitio: pantallas, audio, posición de las ventanas, los
programas cerrados se vuelven a abrir y Steam se cierra.

Se maneja con:

| Canal | Cómo |
|---|---|
| **Mando** | `Back + Start` mantenido abre un menú en la tele; `Back + Start + botón` son atajos |
| **Móvil** | Panel web instalable (PWA) o la app Android, que además enciende el PC |
| **Alexa** | «Alexa, dile a modo sofá que cierre el juego» |
| **Bandeja** | Icono junto al reloj |

No necesita permisos de administrador.

## Descargar

En la [página de releases](https://github.com/jbautistamartin/ModoSofa/releases/latest):
`ModoSofa-Setup-X.Y.Z.exe` para el PC, `modosofa-X.Y.Z.apk` para el móvil, y la Lambda y el modelo
de Alexa. Actualizar es ejecutar el instalador nuevo: conserva la configuración y la vinculación.
Los cambios de cada versión están en [`CHANGELOG.md`](CHANGELOG.md).

## Atajos del mando

| Combinación | Acción |
|---|---|
| `Back + Start` (mantener 0,8 s) | Abrir o cerrar el menú en la tele |
| `Back + Start + B` | Cerrar el juego y volver a ES-DE o Steam |
| `Back + Start + Y` | Traer el juego al frente |
| `Back + Start + X` | Silenciar |
| `Back + Start + ↑ / ↓` | Subir o bajar el volumen |

Se configuran en `Mando.Atajos`. No chocan con los de Steam (botón Guide) ni con los de RetroArch
(L1 + R1 + Start + Select abre su menú; ModoSofá exige la combinación exacta).

## Estructura

```
src/ModoSofa.Nucleo/     Lógica pura y testeable: configuración, estado, acciones, mando, Alexa
src/ModoSofa.Agente/     El ejecutable: bandeja, API, efectos Win32, overlay, servicios
  Win32/                 Pantallas (CCD), audio (Core Audio), ventanas, energía, XInput
  Efectos/               Steam, ES-DE, RetroArch, procesos, tele LG, Wake-on-LAN
  Servicios/             Modo sofá, detección de la tele, guardián de foco, mando, estado
  Interfaz/              Icono de la bandeja, overlay (WebView2), vinculación por QR
  wwwroot/               Panel del móvil (PWA) y menú de la tele
tests/                   Tests del núcleo
alexa/                   Lambda genérica y modelo de interacción es-ES
android/                 App Android: envoltorio de la PWA + Wake-on-LAN
installer/               Instalador (Inno Setup) y los pasos que ejecuta
tools/                   Publicación, instalación desde el repo, firewall, iconos, capturas
docs/                    Plan, arquitectura e instalación
```

## Desarrollo

```powershell
dotnet build ModoSofa.slnx
dotnet test tests/ModoSofa.Nucleo.Tests
dotnet run --project src/ModoSofa.Agente
./tools/publicar.ps1        # dist/: agente autocontenido + Lambda + modelo de Alexa (+ APK si existe)
./release.ps1 -SoloCompilar # todo lo anterior + APK + instalador, con versión en el nombre
./release.ps1               # además etiqueta, sube a GitHub y publica el release
```

Requisitos para compilar: SDK de .NET 11, Python 3, Inno Setup 6
(`winget install JRSoftware.InnoSetup --scope user`) y, para el APK, Android Studio (su JBR) con
el SDK en `%LOCALAPPDATA%\Android\Sdk`.

Instalación y puesta en marcha: [`docs/INSTALACION.md`](docs/INSTALACION.md).
Cómo funciona por dentro: [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md).
Plan y estado de cada fase: [`docs/PLAN.md`](docs/PLAN.md).
