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

# Activa los comandos de red de RetroArch (network_cmd_enable) para que ModoSofá pueda cerrarlo y
# pausarlo limpiamente. Guarda una copia del retroarch.cfg original la primera vez.
param([string]$EsdeExe = "C:\Games\EmulationStation\ES-DE.exe")
. (Join-Path $PSScriptRoot "comun.ps1")

$exes = @(Buscar-RetroArch $EsdeExe)
if ($exes.Count -eq 0) { Escribir-Registro "RetroArch: no se ha encontrado."; exit 0 }
if (Get-Process retroarch -ErrorAction SilentlyContinue) {
    # Con RetroArch abierto, al salir sobrescribiría el cambio con su configuración en memoria.
    Escribir-Registro "RetroArch: está abierto; no se toca su configuración. Ciérralo y vuelve a ejecutar el instalador."
    exit 0
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($exe in $exes) {
    $cfg = Join-Path (Split-Path $exe) "retroarch.cfg"
    if (-not (Test-Path $cfg)) { Escribir-Registro "RetroArch: $exe no tiene retroarch.cfg todavía (se crea al abrirlo)."; continue }
    $texto = [IO.File]::ReadAllText($cfg)
    if ($texto -match '(?m)^network_cmd_enable\s*=\s*"true"') { Escribir-Registro "RetroArch: $cfg ya tenía los comandos de red."; continue }

    $copia = "$cfg.antes-de-modosofa.bak"
    if (-not (Test-Path $copia)) { Copy-Item $cfg $copia }
    $salto = if ($texto -match "`r`n") { "`r`n" } else { "`n" }
    if ($texto -match '(?m)^network_cmd_enable\s*=') {
        $texto = [regex]::Replace($texto, '(?m)^network_cmd_enable\s*=.*$', 'network_cmd_enable = "true"')
    } else {
        $texto = $texto.TrimEnd() + $salto + 'network_cmd_enable = "true"' + $salto
    }
    if ($texto -notmatch '(?m)^network_cmd_port\s*=') { $texto = $texto.TrimEnd() + $salto + 'network_cmd_port = "55355"' + $salto }
    [IO.File]::WriteAllText($cfg, $texto, $utf8)
    Escribir-Registro "RetroArch: comandos de red activados en $cfg (copia en $copia)."
}
