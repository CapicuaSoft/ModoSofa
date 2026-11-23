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

# Se ejecuta elevado (una sola confirmación de administrador):
#  - permite ModoSofá en redes privadas, para que el móvil llegue por la Wi-Fi de casa;
#  - bloquea la entrada a RetroArch: ModoSofá le habla desde el propio PC, nadie más debe poder.
#    Además evita que Windows pregunte por RetroArch al activar sus comandos de red.
param([Parameter(Mandatory)][string]$Programa, [string]$EsdeExe = "C:\Games\EmulationStation\ES-DE.exe")
. (Join-Path $PSScriptRoot "comun.ps1")

Get-NetFirewallRule -DisplayName "ModoSofá*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
# Las que crea Windows al contestar su aviso («modosofa.exe», «ModoSofa», a menudo para redes
# públicas) también se quitan: solo debe quedar el permiso para redes privadas.
Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue |
    Where-Object { $_.Program -like "*\modosofa.exe" } |
    Get-NetFirewallRule -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName "ModoSofá" -Description "Panel del móvil (solo redes privadas)." `
    -Direction Inbound -Program $Programa -Protocol TCP -Profile Private -Action Allow | Out-Null
Escribir-Registro "Firewall: ModoSofá permitido en redes privadas."

foreach ($exe in Buscar-RetroArch $EsdeExe) {
    # También aquí sobran las reglas que creó Windows al preguntar por RetroArch.
    Get-NetFirewallApplicationFilter -Program $exe -ErrorAction SilentlyContinue |
        Get-NetFirewallRule -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName "ModoSofá: bloquear RetroArch" -Description "Los comandos de red de RetroArch solo desde este PC." `
        -Direction Inbound -Program $exe -Action Block | Out-Null
    Escribir-Registro "Firewall: entrada bloqueada para $exe."
}
