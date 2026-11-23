# ModoSofá — Plan de trabajo

Registro del plan acordado y de su estado. Se actualiza al cerrar cada tarea.

## Origen

El PC de trabajo (dos monitores) se usa también para jugar desde el sofá: se desenchufa un
monitor y se conecta la tele LG en su lugar, se juega con un mando (F710 en modo XInput) a través
de Steam Big Picture, que lanza juegos de PC y ES-DE (`C:\Games\EmulationStation\ES-DE.exe`), que
a su vez lanza RetroArch (`C:\Games\EmulationStation\Emulators\RetroArch-Win64`).

AlexaController resolvía parte del problema, pero:

- la cadena Alexa → Lambda → ngrok → API era el único camino además del móvil;
- el modo juegos era **destructivo** (mataba procesos y servicios, cerraba todas las ventanas) y
  podía dejar el PC a medio configurar;
- todo corría **elevado**;
- no gestionaba el audio, la tele, el foco ni la vuelta ordenada al trabajo.

## Decisiones

| Tema | Decisión |
|---|---|
| Usuario de Windows | El mismo para trabajar y jugar; el PC se adapta sin destruir nada |
| Entrada al modo sofá | **Automática al enchufar la tele**, además de mando, móvil y Alexa |
| Ventanas de trabajo | Se minimizan todas (y se restauran); se cierran solo las de una lista |
| Salida | Restaura pantallas, audio, energía y ventanas; relanza lo cerrado; **cierra Steam** |
| Privilegios | Ninguno: el agente corre como usuario normal |
| Mando | `Back` mantenido 2 s abre el menú; sin atajos directos (`Back+Start` lo usa Steam) |
| Móvil | PWA servida por el propio agente + APK envoltorio con Wake-on-LAN |
| Alexa | Imprescindible, fase 3: Lambda genérica + Tailscale Funnel (sin ngrok) |
| Acceso remoto | Tailscale (sin Home Assistant: no hay otro equipo) |
| Tele | LG webOS: encender (WoL), cambiar de entrada, apagar |
| RetroArch | Comandos de red activados (`network_cmd_enable`) para cerrar y pausar limpio |
| Tecnología | .NET 11 (C#) para el agente, web sin framework ni paso de build, Python para la Lambda, Kotlin para el APK |
| Repositorio | `C:\Proyectos\ModoSofa`, git local |

## Fases

Estado: ✅ hecho · 🟡 hecho, pendiente de probar en el equipo real · ⬜ pendiente

### Fase 0 — Base
- ✅ Solución .NET: `CapicuaSoft.ModoSofa.Nucleo` (lógica pura), `CapicuaSoft.ModoSofa.Agente` (ejecutable), tests
- ✅ Configuración en `%APPDATA%\ModoSofa\config.json` (se crea con valores por defecto)
- ✅ Estado persistente con todo lo necesario para deshacer (`estado.json`)
- ✅ Registro de acciones único (mando, móvil, overlay, Alexa y bandeja llaman a lo mismo)
- ✅ Log a fichero diario + memoria (visible en el móvil, en vivo)
- ✅ Icono de bandeja, instancia única, inicio con Windows (sin elevación)
- ✅ API HTTP con token + eventos en vivo (SSE)

### Fase 1 — MVP: modo sofá
- 🟡 Detección de la tele por nombre de pantalla (EDID) con anti-rebote → entra/sale solo
- 🟡 Pantallas: solo la tele al entrar; restaurar la topología al salir
- 🟡 Audio: salida por la tele al entrar (y volumen inicial opcional); restaurar al salir
- 🟡 Ventanas: cerrar la lista configurada (y relanzarlas al salir, también apps MSIX), minimizar el resto y restaurar su posición
- 🟡 Energía: plan de energía opcional + impedir suspensión
- 🟡 Steam en Big Picture con foco; cerrar Steam al salir
- 🟡 Cerrar juego escalonado (RetroArch por comando de red → ventana → proceso) y devolver el foco a ES-DE/Steam
- 🟡 Enfocar juego, volumen, ES-DE, suspender/reiniciar/apagar
- 🟡 Recuperación: si el PC se reinicia en modo sofá sin la tele, se revierte al arrancar
- 🟡 PWA móvil: estado en vivo, botones, log, ajustes, diagnóstico
- 🟡 Vinculación del móvil por QR

### Fase 2 — Mando y tele
- 🟡 Lectura XInput en segundo plano (no necesita foco)
- ✅ Back mantenido 2 s → menú en la tele; atajos directos configurables (vacíos por defecto)
- 🟡 Overlay en la tele (WebView2, no roba el foco) navegable con el mando
- 🟡 Pausa automática de RetroArch mientras el overlay está abierto
- 🟡 Guardián de foco: si una app de trabajo se pone delante, se minimiza y vuelve el juego

### Fase 3 — Alexa y acceso remoto
- 🟡 Endpoint `/api/alexa` con token propio (confirmación para apagar/reiniciar)
- 🟡 Lambda genérica (Python 3.13): solo reenvía el intent
- 🟡 Modelo de interacción `es-ES` (invocación «modo sofá»)
- 🟡 Guía de Tailscale (`serve` para el móvil, `funnel` solo para Alexa)

### Fase 4 — Extras
- 🟡 Tele LG webOS: encender por WoL, cambiar a la entrada HDMI, apagar al salir
- 🟡 APK Android: envoltorio de la PWA + Wake-on-LAN + pantalla sin conexión. Compilado y firmado
  con `capicua.jks` (mismo certificado que GameController); falta instalarlo en el móvil
- ⬜ Modo «No molestar» de Windows: no hay API pública; queda fuera

## Verificado en este PC (22/11/2026)

- Compila sin avisos; 19 tests del núcleo en verde; `tools/publicar.ps1` genera `dist/` y el
  ejecutable autocontenido arranca y sirve la web.
- API: rechaza sin token (401), el token del móvil no vale en `/api/alexa`.
- Detecta los 2 mandos, la salida de audio, Steam, ES-DE y la red (MAC por cable).
- El monitor actual no anuncia nombre EDID: se identifica como `(sin nombre) [AGO]`.
- Overlay abierto por la API: se dibuja encima **sin robar el foco**.
- Panel del móvil (412 px) y overlay (1190×842) revisados con capturas: sin errores de consola.
- Lambda ejecutada en local contra el agente: lanzamiento, ayuda, confirmación de apagar,
  cancelación, intent desconocido y token incorrecto.
- RetroArch: `network_cmd_enable = "true"` (copia en `retroarch.cfg.antes-de-modosofa.bak`).

### Ciclo completo en el PC real (sin tele: el monitor `[AGO]` hizo de tele y los altavoces de audio de la tele)

- Detección automática → entrada en modo sofá sin intervención.
- Pantalla única (conservando el modo), audio y volumen inicial, Teams cerrado (y terminado al
  no cerrarse en 5 s), ventanas minimizadas, suspensión inhibida, Steam arrancado en Big Picture.
- Guardián: Notepad++ forzado al frente → minimizado en < 1 s.
- RetroArch abierto → detectado, responde a `GET_STATUS`, «Cerrar juego» lo cierra con `QUIT`;
  al salir reescribe su configuración y `network_cmd_enable` sigue en `true`.
- Overlay visible encima de Big Picture a pantalla completa.
- Volver a trabajar: Steam cerrado, pantallas, audio y volumen restaurados, 3 de 3 / 4 de 4
  ventanas en su sitio, Teams relanzado (app MSIX, por AUMID).
- Cuelgue simulado en modo sofá + tele ausente al rearrancar → se deshace todo solo.

Arreglado durante la prueba:
- El título de Big Picture en español lleva un espacio no separable: no se detectaba la ventana.
- Lanzar `steam.exe` tarda ~15 s en este PC (medido fuera de ModoSofá; cualquier otro exe
  arranca en < 1,5 s; probablemente el antivirus). Si Big Picture ya está abierto, ahora solo se
  le da el foco. Con Steam cerrado la entrada completa tarda ~37 s.

Efecto secundario: al activar los comandos de red, Windows pregunta si `retroarch.exe` puede
usar la red. Hay que responder **Cancelar**: ModoSofá le habla desde el propio PC, y así nadie de
la red puede enviarle órdenes.

**No probado todavía:** con dos pantallas reales (tele + monitor), audio HDMI de la tele, atajos
pulsados en el mando físico, tele LG, Wake-on-LAN, Tailscale y el skill real de Alexa.

### Hecho después (22/11/2026, por la tarde)

- SDK de Android instalado en `%LOCALAPPDATA%\Android\Sdk` (plataforma 35, build-tools 35.0.0,
  platform-tools). Es la ruta que espera también GameController.
- APK release firmado (`dist/modosofa.apk`). El lint de release está desactivado: AGP 8.5 se cae
  con el JDK 25 del JBR de Android Studio.
- 35 tests del núcleo (EDID, Wake-on-LAN y RetroArch extraídos del agente) y 7 de la Lambda.
- ModoSofá instalado con `tools/instalar.ps1` en `%LOCALAPPDATA%\Programs\ModoSofa`, con inicio
  con Windows y configuración por defecto (entra solo al detectar «LG TV»).
- AlexaController **sigue activo** a propósito: es la única vía de voz hasta probar el skill nuevo.

### Releases (22/11/2026, noche)

- Repositorio público: https://github.com/CapicuaSoft/ModoSofa (historial revisado: sin la MAC
  ni los tokens del PC).
- Instalador de Inno Setup sin elevación (`installer/`), probado en este PC: instalación
  silenciosa, desinstalación (conserva la configuración) y reinstalación con el mismo token.
- `release.ps1` publica el release; **v0.1.0 publicado** con instalador, zip portable, APK firmado,
  Lambda, modelo de Alexa y SHA256SUMS.
- Aviso de versión nueva (bandeja y móvil) contra la API de releases de GitHub.

### 0.2.0: autoactualización, donación y licencia (22/11/2026, noche)

- Mismo patrón que SilentRunner y SMSRelay: comprobación diaria con ETag y aviso recordado,
  «Acerca de» con «Invitar a un café» (también en el menú de la bandeja) y LGPL-2.1 con cabecera
  en cada fichero.
- PC: se actualiza solo con el instalador del release, verificado por SHA-256, solo en modo
  trabajo. Probado en este PC: instalador 0.2.0 sobre 0.1.0 con los parámetros del actualizador →
  0.2.0 en marcha, mismo token, inicio con Windows conservado y sin pedir administrador.
- Android: busca, descarga y verifica el APK; Android pide confirmar.
- **Pendiente de publicar**: commit `0.2.0` solo en local y artefactos en `dist/`. Publicar exige
  `git push` + `./release.ps1` (el script se niega con el árbol sucio: ejecutar con todo
  confirmado). Hasta entonces no se puede probar la cadena completa contra GitHub
  (descarga + verificación + instalación automática).

### Pruebas finales con la tele (22/11/2026, 20:00–20:45, con el usuario delante)

Superadas: entrada automática al enchufar la tele LG («LG TV [GSM]»), pantalla única, audio por
la tele, Teams cerrado y ventanas minimizadas, Big Picture, menú con el mando encima de Steam y de
ES-DE sin que el juego reciba las pulsaciones, menú de RetroArch con Start mantenido, «Cerrar
juego» en RetroArch, ES-DE y un juego de Steam (con confirmación), vuelta al trabajo con los dos
monitores, audio, ventanas y Teams restaurados, y el móvil por Wi-Fi (estado y log en vivo).

Arreglado durante las pruebas:
- El audio HDMI de la tele aparece unos segundos después de la imagen: ahora se espera.
- `Back+Start` lo usa Steam (menú y teclado en pantalla) → `Back` mantenido 2 s, sin atajos.
- Steam Input duplica el mando y el menú se abría y cerraba a la vez → un detector sobre la unión.
- El menú no tomaba el foco: ES-DE lo tapaba y el juego seguía recibiendo botones → toma el foco.
- «Cerrar juego» ahora cierra lo que hay delante (ES-DE incluido); juegos de Steam con confirmación.
- La vuelta restauraba «monitor + tele» y dejaba apagado el segundo monitor → se extiende si
  cambiaron las pantallas conectadas.
- El guardián actuaba durante la vuelta al trabajo → se pausa al empezar.
- RetroArch: menú con Start mantenido 2 s y sin combinación de salida (copia
  `retroarch.cfg.antes-combos-20261122-2034.bak`).

Pendiente: la red de casa está marcada como **pública** (Windows creó reglas «Permitir» para
redes públicas); conviene marcarla como privada. App Android, tele LG por red, Tailscale y Alexa
sin probar.

## Pendiente de hacer en el equipo real
1. Responder a los avisos del firewall: **Permitir** ModoSofá en redes privadas (o
   `tools/permitir-firewall.ps1`) y **Cancelar** el de RetroArch.
2. Con la tele conectada, revisar `/api/diagnostico` (nombres de pantallas y audio) para ajustar
   `Tele.NombrePantalla` y `Audio.DispositivoSofa`.
3. Ajustar `Ventanas.CerrarProcesos` con los programas de trabajo reales.
4. Probar el ciclo completo enchufando la tele, y los atajos con el mando físico (comprobar que
   Steam Input no interfiere).
5. Instalar `dist/modosofa.apk` en el móvil y activar Wake-on-LAN en la BIOS y en la tarjeta de red.
6. Emparejar la tele LG (aceptar el aviso en pantalla la primera vez).
7. Tailscale, Lambda y skill de Alexa según `docs/INSTALACION.md`.
8. Cuando el skill nuevo funcione: retirar la tarea programada y el túnel ngrok de AlexaController.
