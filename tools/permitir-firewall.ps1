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
