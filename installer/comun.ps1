# ModoSofá - Convierte el PC de trabajo en una consola de salón.
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

# Funciones compartidas por los pasos del instalador.

$Registro = Join-Path $env:LOCALAPPDATA "ModoSofa\instalacion.log"

function Escribir-Registro([string]$Texto) {
    New-Item -ItemType Directory -Force (Split-Path $Registro) | Out-Null
    Add-Content -Path $Registro -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Texto" -Encoding UTF8
}

# Carpetas donde suele estar RetroArch: la de ES-DE portable, la instalación normal, Steam y C:\Games.
function Buscar-RetroArch([string]$EsdeExe = "C:\Games\EmulationStation\ES-DE.exe") {
    $candidatas = New-Object System.Collections.Generic.List[string]
    if ($EsdeExe) { $candidatas.Add((Join-Path (Split-Path $EsdeExe) "Emulators")) }
    $candidatas.Add((Join-Path $env:APPDATA "RetroArch"))
    $candidatas.Add("C:\RetroArch-Win64"); $candidatas.Add("C:\RetroArch")
    $candidatas.Add((Join-Path $env:ProgramFiles "RetroArch"))
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steam) { $candidatas.Add((Join-Path ($steam -replace '/', '\') "steamapps\common\RetroArch")) }
    $candidatas.Add("C:\Games")

    $candidatas | Where-Object { Test-Path $_ } |
        ForEach-Object { Get-ChildItem $_ -Filter retroarch.exe -Recurse -Depth 3 -ErrorAction SilentlyContinue } |
        Select-Object -ExpandProperty FullName -Unique
}
