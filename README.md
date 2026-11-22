# ModoSofá

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
tools/                   Firewall, publicación, iconos, capturas de prueba
docs/                    Plan, arquitectura e instalación
```

## Desarrollo

```powershell
dotnet build ModoSofa.slnx
dotnet test tests/ModoSofa.Nucleo.Tests
dotnet run --project src/ModoSofa.Agente
./tools/publicar.ps1        # dist/: agente autocontenido + Lambda + modelo de Alexa (+ APK si existe)
```

Instalación y puesta en marcha: [`docs/INSTALACION.md`](docs/INSTALACION.md).
Cómo funciona por dentro: [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md).
Plan y estado de cada fase: [`docs/PLAN.md`](docs/PLAN.md).
