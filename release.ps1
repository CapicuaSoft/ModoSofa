# ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
# Copyright (C) 2026  José Luis Bautista Martín
#
# This library is free software; you can redistribute it and/or
# modify it under the terms of the GNU Lesser General Public
# License as published by the Free Software Foundation; either
# version 2.1 of the License, or (at your option) any later version.
#
# This library is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
# Lesser General Public License for more details.
#
# You should have received a copy of the GNU Lesser General Public
# License along with this library; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

# Genera un release de ModoSofá y lo publica en GitHub.
#
#   ./release.ps1 -SoloCompilar   # solo genera los artefactos en dist/ (sin git ni GitHub)
#   ./release.ps1                 # además etiqueta vX.Y.Z, sube a GitHub y publica el release
#
# Antes: subir <Version> en Directory.Build.props y versionName/versionCode en
# android/app/build.gradle.kts, pasar lo de [No publicado] de CHANGELOG.md a la versión nueva y
# reescribir docs/NOTAS_RELEASE.md (son las notas del release en GitHub).
param([switch]$SoloCompilar)
$ErrorActionPreference = "Stop"
$raiz = $PSScriptRoot
$dist = Join-Path $raiz "dist"
function Fallar([string]$m) { Write-Host "✖ $m" -ForegroundColor Red; exit 1 }
function Paso([string]$m) { Write-Host "▶ $m" -ForegroundColor Cyan }

# --- Versión: una sola fuente ----------------------------------------------------------------
$version = ([xml](Get-Content (Join-Path $raiz "Directory.Build.props"))).Project.PropertyGroup.Version
if (-not $version) { Fallar "No hay <Version> en Directory.Build.props." }
$gradle = Get-Content (Join-Path $raiz "android\app\build.gradle.kts") -Raw
if ($gradle -notmatch "versionName = `"$([regex]::Escape($version))`"") { Fallar "versionName del APK no es $version (android/app/build.gradle.kts)." }
if ((Get-Content (Join-Path $raiz "CHANGELOG.md") -Raw) -notmatch "## \[$([regex]::Escape($version))\]") { Fallar "CHANGELOG.md no tiene la sección [$version]." }
$notas = Join-Path $raiz "docs\NOTAS_RELEASE.md"
if (-not (Test-Path $notas)) { Fallar "Falta docs/NOTAS_RELEASE.md." }
if ((Get-Content $notas -Raw) -notmatch [regex]::Escape($version)) { Fallar "docs/NOTAS_RELEASE.md no menciona la versión $version." }
$etiqueta = "v$version"
Paso "ModoSofá $version"

if (-not $SoloCompilar) {
    if (git -C $raiz status --porcelain) { Fallar "Hay cambios sin confirmar." }
    if ((git -C $raiz branch --show-current) -ne "main") { Fallar "El release se hace desde main." }
    if (git -C $raiz tag --list $etiqueta) { Fallar "La etiqueta $etiqueta ya existe." }
    gh auth status *> $null; if ($LASTEXITCODE -ne 0) { Fallar "gh no tiene sesión iniciada." }
}

# --- APK firmado -----------------------------------------------------------------------------
Paso "APK"
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
Push-Location (Join-Path $raiz "android")
.\gradlew.bat assembleRelease --no-daemon -q
$codigo = $LASTEXITCODE
Pop-Location
if ($codigo -ne 0) { Fallar "Falló la compilación del APK." }

# --- Agente, Lambda, modelo y tests ----------------------------------------------------------
Paso "Tests y publicación"
& (Join-Path $raiz "tools\publicar.ps1")
if (-not (Test-Path (Join-Path $dist "modosofa.apk"))) { Fallar "No se generó el APK." }

# --- Instalador ------------------------------------------------------------------------------
Paso "Instalador"
$iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { Fallar "Falta Inno Setup 6 (winget install JRSoftware.InnoSetup --scope user)." }
& $iscc /Q "/DVersion=$version" (Join-Path $raiz "installer\ModoSofa.iss")
if ($LASTEXITCODE -ne 0) { Fallar "Falló la compilación del instalador." }

# --- Artefactos con versión ------------------------------------------------------------------
Paso "Artefactos"
$artefactos = @(
    (Join-Path $dist "ModoSofa-Setup-$version.exe")
)
Compress-Archive (Join-Path $dist "ModoSofa\*") (Join-Path $dist "ModoSofa-$version-win-x64-portable.zip") -Force
$artefactos += Join-Path $dist "ModoSofa-$version-win-x64-portable.zip"
Move-Item (Join-Path $dist "modosofa.apk") (Join-Path $dist "modosofa-$version.apk") -Force
$artefactos += Join-Path $dist "modosofa-$version.apk"
Move-Item (Join-Path $dist "lambda-modosofa.zip") (Join-Path $dist "lambda-modosofa-$version.zip") -Force
$artefactos += Join-Path $dist "lambda-modosofa-$version.zip"
Move-Item (Join-Path $dist "modelo-alexa-es-ES.json") (Join-Path $dist "modelo-alexa-es-ES-$version.json") -Force
$artefactos += Join-Path $dist "modelo-alexa-es-ES-$version.json"

$sumas = Join-Path $dist "SHA256SUMS.txt"
# Finales de línea LF: con CRLF, «sha256sum -c» no encuentra los ficheros.
$lineas = $artefactos | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLower(), (Split-Path $_ -Leaf) }
[IO.File]::WriteAllText($sumas, ($lineas -join "`n") + "`n", [Text.Encoding]::ASCII)
$artefactos += $sumas
$artefactos | Get-Item | Format-Table Name, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }

if ($SoloCompilar) { Paso "Listo (solo compilar)."; exit 0 }

# --- Publicar --------------------------------------------------------------------------------
Paso "Publicando $etiqueta"
git -C $raiz tag -a $etiqueta -m "ModoSofá $version"
git -C $raiz push origin main
git -C $raiz push origin $etiqueta
gh release create $etiqueta @artefactos --repo CapicuaSoft/ModoSofa --title "ModoSofá $version" --notes-file $notas
if ($LASTEXITCODE -ne 0) { Fallar "gh release create falló (la etiqueta ya está subida)." }
Paso "Publicado: https://github.com/CapicuaSoft/ModoSofa/releases/tag/$etiqueta"
