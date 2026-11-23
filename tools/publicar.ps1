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

# Genera dist/: el agente autocontenido (no necesita .NET instalado) y el zip de la Lambda.
$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $raiz "dist"
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null

dotnet test (Join-Path $raiz "tests\CapicuaSoft.ModoSofa.Nucleo.Tests") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Los tests fallan." }
Push-Location (Join-Path $raiz "alexa\lambda")
python -m unittest -q
$codigo = $LASTEXITCODE
Remove-Item __pycache__ -Recurse -Force -ErrorAction SilentlyContinue
Pop-Location
if ($codigo -ne 0) { throw "Los tests de la Lambda fallan." }

dotnet publish (Join-Path $raiz "src\CapicuaSoft.ModoSofa.Agente") -c Release -r win-x64 --self-contained `
    -p:PublishReadyToRun=true -o (Join-Path $dist "ModoSofa") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación." }

Compress-Archive (Join-Path $raiz "alexa\lambda\lambda_function.py") (Join-Path $dist "lambda-modosofa.zip")
Copy-Item (Join-Path $raiz "alexa\modelo\es-ES.json") (Join-Path $dist "modelo-alexa-es-ES.json")

$apk = Join-Path $raiz "android\app\build\outputs\apk\release\app-release.apk"
if (Test-Path $apk) { Copy-Item $apk (Join-Path $dist "modosofa.apk") }
else { Write-Warning "No hay APK (cd android; ./gradlew assembleRelease). Se omite." }

Get-ChildItem $dist | Format-Table Name, Length
