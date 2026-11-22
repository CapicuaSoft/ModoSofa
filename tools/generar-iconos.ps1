# Genera los iconos de la PWA (mismo dibujo que el icono de la bandeja, Interfaz/Icono.cs).
param([string]$Destino = "$PSScriptRoot\..\src\ModoSofa.Agente\wwwroot")
Add-Type -AssemblyName System.Drawing
foreach ($lado in 192, 512) {
    $bmp = New-Object System.Drawing.Bitmap $lado, $lado
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(20, 22, 31))
    $k = $lado / 32.0
    $blanco = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(124, 108, 242))
    # Zona segura de los iconos «maskable»: se dibuja dentro del 70 % central.
    $o = $lado * 0.15; $e = $k * 0.7
    foreach ($r in @(@(9,9,14,8), @(5,14,5,9), @(22,14,5,9), @(9,18,14,5), @(7,23,3,3), @(22,23,3,3))) {
        $g.FillRectangle($blanco, [single]($o + $r[0]*$e), [single]($o + $r[1]*$e), [single]($r[2]*$e), [single]($r[3]*$e))
    }
    $bmp.Save("$Destino\icono-$lado.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
