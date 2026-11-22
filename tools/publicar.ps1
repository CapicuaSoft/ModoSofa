# Genera dist/: el agente autocontenido (no necesita .NET instalado) y el zip de la Lambda.
$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $raiz "dist"
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null

dotnet test (Join-Path $raiz "tests\ModoSofa.Nucleo.Tests") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Los tests fallan." }

dotnet publish (Join-Path $raiz "src\ModoSofa.Agente") -c Release -r win-x64 --self-contained `
    -p:PublishReadyToRun=true -o (Join-Path $dist "ModoSofa") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación." }

Compress-Archive (Join-Path $raiz "alexa\lambda\lambda_function.py") (Join-Path $dist "lambda-modosofa.zip")
Copy-Item (Join-Path $raiz "alexa\modelo\es-ES.json") (Join-Path $dist "modelo-alexa-es-ES.json")

$apk = Join-Path $raiz "android\app\build\outputs\apk\debug\app-debug.apk"
if (Test-Path $apk) { Copy-Item $apk (Join-Path $dist "modosofa.apk") }
else { Write-Warning "No hay APK (cd android; ./gradlew assembleDebug). Se omite." }

Get-ChildItem $dist | Format-Table Name, Length
