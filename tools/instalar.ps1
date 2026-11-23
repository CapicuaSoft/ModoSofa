# Instala (o actualiza) ModoSofá desde dist/ en %LOCALAPPDATA%\Programs\ModoSofa, lo registra para
# arrancar con Windows (sin elevación) y lo inicia. Ejecutar antes tools/publicar.ps1.
$ErrorActionPreference = "Stop"
$origen = Join-Path (Split-Path $PSScriptRoot -Parent) "dist\ModoSofa"
$destino = Join-Path $env:LOCALAPPDATA "Programs\ModoSofa"
if (-not (Test-Path (Join-Path $origen "ModoSofa.exe"))) { throw "No hay dist\ModoSofa: ejecuta antes tools/publicar.ps1." }

# Detener la instancia en marcha. Se cierra sin deshacer el modo sofá: el estado queda en disco y
# la nueva versión lo recupera al arrancar.
$enMarcha = Get-Process ModoSofa -ErrorAction SilentlyContinue
if ($enMarcha) {
    $enMarcha | Stop-Process -Force
    $enMarcha | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force $destino | Out-Null
robocopy $origen $destino /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falló ($LASTEXITCODE)." }

$exe = Join-Path $destino "ModoSofa.exe"
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ModoSofa" -Value "`"$exe`""
Start-Process $exe
Write-Host "ModoSofá instalado en $destino, arrancando con Windows y en marcha."
