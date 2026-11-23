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
