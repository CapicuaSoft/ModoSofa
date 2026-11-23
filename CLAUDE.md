# CLAUDE.md

Guía para trabajar en este repositorio. Todo el código, los comentarios y los mensajes van en español.

## Qué es

ModoSofá convierte el PC de trabajo en una consola de salón (tele como pantalla única, audio por
la tele, ventanas de trabajo minimizadas, Steam Big Picture) y lo restaura al terminar. Lo
disparan la conexión de la tele, el mando, el móvil (PWA / app Android) y Alexa. Sucesor de
`C:\Proyectos\AlexaController`. El plan y su estado están en `docs/PLAN.md`; la arquitectura, en
`docs/ARQUITECTURA.md`.

## Comandos

```powershell
dotnet build ModoSofa.slnx
dotnet test tests/ModoSofa.Nucleo.Tests
dotnet run --project src/ModoSofa.Agente
cd alexa/lambda; python -m unittest          # tests de la Lambda
./tools/publicar.ps1                         # tests + dist/ (agente, Lambda, modelo, APK)
./tools/instalar.ps1                         # dist/ → %LOCALAPPDATA%\Programs\ModoSofa + inicio con Windows
cd android; ./gradlew assembleRelease        # SDK en %LOCALAPPDATA%\Android\Sdk; JAVA_HOME = jbr de Android Studio
./release.ps1 -SoloCompilar                  # APK + tests + dist/ + instalador (Inno Setup) con versión
./release.ps1                                # además etiqueta vX.Y.Z, push y gh release create
node tools/captura.mjs <url> <png> [ancho] [alto] [esperaMs]   # captura con Edge headless + errores de consola
```

`nuget.config` fija nuget.org porque esta máquina no lo tiene como origen global.

## Reglas

- **Toda acción pasa por `RegistroAcciones`** (`Servicios/CatalogoAcciones.cs`). Nunca llames a los
  efectos directamente desde la API, el mando o la bandeja.
- **No destructivo:** el modo sofá minimiza y recuerda. Solo se cierra lo de `Ventanas.CerrarProcesos`.
- **Apunta antes de cambiar:** cualquier cambio nuevo del modo sofá guarda en `EstadoPersistente`
  lo necesario para deshacerlo *antes* de aplicarlo, y `SalirAsync` lo deshace.
- **Sin elevación:** el manifiesto es `asInvoker`. No añadas nada que requiera administrador.
- La lógica que no depende de Windows va en `ModoSofa.Nucleo` y lleva tests.
- La interfaz WinForms vive en el hilo STA de `Bandeja`; desde otros hilos, pasa por `EnUi`.
- **Versión:** única fuente `<Version>` en `Directory.Build.props`; `versionName` del APK debe
  coincidir (release.ps1 lo comprueba). Antes de un release: CHANGELOG.md y docs/NOTAS_RELEASE.md.
- Los `.ps1` y el `.iss` van en **UTF-8 con BOM**: Windows PowerShell 5.1 e Inno Setup leen sin
  BOM como ANSI y estropean las tildes (p. ej. el nombre de la regla de firewall).
- Puerto por defecto 5790 (AlexaController usa el 5780).
- **Probar en el equipo real con cuidado:** entrar en modo sofá cierra programas (Outlook, Teams)
  y minimiza ventanas del usuario. No lo dispares sin avisar.
