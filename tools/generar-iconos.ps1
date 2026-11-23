# ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
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

# Genera los iconos a partir del mismo dibujo que el de la bandeja (Interfaz/Icono.cs):
#  - wwwroot/icono-192.png y icono-512.png (PWA, con margen para iconos «maskable»)
#  - src/CapicuaSoft.ModoSofa.Agente/ModoSofa.ico (ejecutable e instalador; PNG dentro del ICO, Vista+)
param([string]$Raiz = (Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing

function Dibujar([int]$lado, [double]$margen, [bool]$fondoRedondeado) {
    $bmp = New-Object System.Drawing.Bitmap $lado, $lado
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $violeta = [System.Drawing.Color]::FromArgb(124, 108, 242)
    if ($fondoRedondeado) {
        $g.Clear([System.Drawing.Color]::Transparent)
        $r = [single]($lado * 0.22); $d = 2 * $r; $w = [single]($lado - 1)
        $ruta = New-Object System.Drawing.Drawing2D.GraphicsPath
        $ruta.AddArc(0, 0, $d, $d, 180, 90); $ruta.AddArc($w - $d, 0, $d, $d, 270, 90)
        $ruta.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $ruta.AddArc(0, $w - $d, $d, $d, 90, 90); $ruta.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush $violeta), $ruta)
        $pincel = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    } else {
        $g.Clear([System.Drawing.Color]::FromArgb(20, 22, 31))
        $pincel = New-Object System.Drawing.SolidBrush $violeta
    }
    $o = $lado * $margen; $e = ($lado - 2 * $o) / 32.0
    foreach ($r in @(@(9,9,14,8), @(5,14,5,9), @(22,14,5,9), @(9,18,14,5), @(7,23,3,3), @(22,23,3,3))) {
        $g.FillRectangle($pincel, [single]($o + $r[0]*$e), [single]($o + $r[1]*$e), [single]($r[2]*$e), [single]($r[3]*$e))
    }
    $g.Dispose()
    return $bmp
}

$web = Join-Path $Raiz "src\CapicuaSoft.ModoSofa.Agente\wwwroot"
foreach ($lado in 192, 512) {
    $b = Dibujar $lado 0.15 $false
    $b.Save((Join-Path $web "icono-$lado.png"), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# ICO: cabecera + directorio + imágenes. 256 px en PNG; el resto en DIB de 32 bits (BGRA de abajo
# arriba + máscara AND), que es lo que leen todas las versiones de Windows y .NET.
function Dib([System.Drawing.Bitmap]$b) {
    $t = $b.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([uint32]40); $w.Write([int32]$t); $w.Write([int32]($t * 2)); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $t - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $t; $x++) { $c = $b.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) }
    }
    $fila = [math]::Ceiling($t / 32) * 4
    $w.Write((New-Object byte[] ($fila * $t)))   # máscara AND vacía: manda el canal alfa
    $w.Flush()
    return ,$ms.ToArray()
}
$tamanos = 16, 24, 32, 48, 64, 128, 256
$imagenes = foreach ($t in $tamanos) {
    $b = Dibujar $t 0.0 $true
    if ($t -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $datos = $ms.ToArray()
    } else {
        $datos = Dib $b
    }
    $b.Dispose()
    ,$datos
}
$salida = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $salida
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanos.Count)
$desplazamiento = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $lado = if ($t -ge 256) { 0 } else { $t }
    $w.Write([byte]$lado); $w.Write([byte]$lado); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$imagenes[$i].Length); $w.Write([uint32]$desplazamiento)
    $desplazamiento += $imagenes[$i].Length
}
foreach ($img in $imagenes) { $w.Write($img) }
[System.IO.File]::WriteAllBytes((Join-Path $Raiz "src\CapicuaSoft.ModoSofa.Agente\ModoSofa.ico"), $salida.ToArray())
Write-Host "Iconos generados."
