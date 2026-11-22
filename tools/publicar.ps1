# Genera dist/: el agente autocontenido (no necesita .NET instalado) y el zip de la Lambda.
$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $raiz "dist"
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null

dotnet test (Join-Path $raiz "tests\ModoSofa.Nucleo.Tests") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Los tests fallan." }
Push-Location (Join-Path $raiz "alexa\lambda")
python -m unittest -q
$codigo = $LASTEXITCODE
Remove-Item __pycache__ -Recurse -Force -ErrorAction SilentlyContinue
Pop-Location
if ($codigo -ne 0) { throw "Los tests de la Lambda fallan." }

dotnet publish (Join-Path $raiz "src\ModoSofa.Agente") -c Release -r win-x64 --self-contained `
    -p:PublishReadyToRun=true -o (Join-Path $dist "ModoSofa") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación." }

Compress-Archive (Join-Path $raiz "alexa\lambda\lambda_function.py") (Join-Path $dist "lambda-modosofa.zip")
Copy-Item (Join-Path $raiz "alexa\modelo\es-ES.json") (Join-Path $dist "modelo-alexa-es-ES.json")

$apk = Join-Path $raiz "android\app\build\outputs\apk\release\app-release.apk"
if (Test-Path $apk) { Copy-Item $apk (Join-Path $dist "modosofa.apk") }
else { Write-Warning "No hay APK (cd android; ./gradlew assembleRelease). Se omite." }

Get-ChildItem $dist | Format-Table Name, Length
