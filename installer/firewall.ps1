# Se ejecuta elevado (una sola confirmación de administrador):
#  - permite ModoSofá en redes privadas, para que el móvil llegue por la Wi-Fi de casa;
#  - bloquea la entrada a RetroArch: ModoSofá le habla desde el propio PC, nadie más debe poder.
#    Además evita que Windows pregunte por RetroArch al activar sus comandos de red.
param([Parameter(Mandatory)][string]$Programa, [string]$EsdeExe = "C:\Games\EmulationStation\ES-DE.exe")
. (Join-Path $PSScriptRoot "comun.ps1")

Get-NetFirewallRule -DisplayName "ModoSofá*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName "ModoSofá" -Description "Panel del móvil (solo redes privadas)." `
    -Direction Inbound -Program $Programa -Protocol TCP -Profile Private -Action Allow | Out-Null
Escribir-Registro "Firewall: ModoSofá permitido en redes privadas."

foreach ($exe in Buscar-RetroArch $EsdeExe) {
    New-NetFirewallRule -DisplayName "ModoSofá: bloquear RetroArch" -Description "Los comandos de red de RetroArch solo desde este PC." `
        -Direction Inbound -Program $exe -Action Block | Out-Null
    Escribir-Registro "Firewall: entrada bloqueada para $exe."
}
