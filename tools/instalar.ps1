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

# Instala (o actualiza) en este PC lo que hay en dist\ModoSofa usando el instalador, exactamente
# como lo hace la autoactualización: en silencio, sin repetir las tareas que piden administrador,
# en la carpeta donde ya estuviera instalado y volviendo a abrir ModoSofá. Ejecutar antes
# tools\publicar.ps1.
$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $raiz "dist\ModoSofa\ModoSofa.exe"))) { throw "No hay dist\ModoSofa: ejecuta antes tools\publicar.ps1." }

$version = ([xml](Get-Content (Join-Path $raiz "Directory.Build.props"))).Project.PropertyGroup.Version
$iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw "Falta Inno Setup 6 (winget install JRSoftware.InnoSetup --scope user)." }

& $iscc /Q "/DVersion=$version" (Join-Path $raiz "installer\ModoSofa.iss")
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación del instalador." }

$instalador = Join-Path $raiz "dist\ModoSofa-Setup-$version.exe"
# Sin -Wait: Start-Process -Wait espera también a los hijos, y el instalador deja ModoSofá abierto.
$p = Start-Process $instalador -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MERGETASKS="!firewall,!retroarch,!autologon"' -PassThru
$p.WaitForExit()
if ($p.ExitCode -ne 0) { throw "El instalador devolvió $($p.ExitCode)." }

$clave = Get-ChildItem "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall" | Get-ItemProperty |
    Where-Object { $_.DisplayName -like "ModoSof*" } | Select-Object -First 1
Write-Host "ModoSofá $($clave.DisplayVersion) instalado en $($clave.InstallLocation) (editor: $($clave.Publisher))."
