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
