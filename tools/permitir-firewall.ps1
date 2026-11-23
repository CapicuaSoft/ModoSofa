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

# Permite que el móvil llegue al agente por la red de casa. Ejecutar UNA vez como administrador.
# Solo abre el puerto en redes privadas (la Wi-Fi de casa), nunca en redes públicas.
param([int]$Puerto = 5790)

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Puerto $Puerto"
    return
}

Remove-NetFirewallRule -DisplayName "ModoSofá" -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName "ModoSofá" -Direction Inbound -Protocol TCP -LocalPort $Puerto `
    -Profile Private -Action Allow | Out-Null
Write-Host "Regla creada: TCP $Puerto entrante, solo en redes privadas."
Write-Host "Comprueba que tu Wi-Fi está marcada como red privada (Configuración > Red e Internet)."
Read-Host "Pulsa Intro para cerrar"
