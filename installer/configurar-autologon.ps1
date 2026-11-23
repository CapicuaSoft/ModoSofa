# Inicio de sesión automático con Autologon de Sysinternals (Microsoft), que guarda la contraseña
# cifrada como secreto LSA. No se redistribuye: se descarga de Microsoft y se verifica su firma.
. (Join-Path $PSScriptRoot "comun.ps1")
Add-Type -AssemblyName System.Windows.Forms
function Avisar([string]$t, [string]$icono = "Information") {
    [void][System.Windows.Forms.MessageBox]::Show($t, "ModoSofá — inicio de sesión automático", "OK", $icono)
}

# Windows 11 con «solo Windows Hello» activado no admite el inicio automático con contraseña.
$sinContrasena = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device" -ErrorAction SilentlyContinue).DevicePasswordLessBuildVersion
if ($sinContrasena -eq 2) {
    Avisar ("Antes hay que desactivar en Configuración > Cuentas > Opciones de inicio de sesión:`n" +
            "«Para mejorar la seguridad, permitir solo el inicio de sesión de Windows Hello».`n`n" +
            "Después, ejecuta de nuevo el instalador con esta opción marcada.") "Warning"
    Escribir-Registro "Autologon: cancelado (Windows Hello obligatorio activado)."
    exit 0
}

$destino = Join-Path $env:TEMP "Autologon64.exe"
try {
    Invoke-WebRequest "https://live.sysinternals.com/Autologon64.exe" -OutFile $destino -UseBasicParsing
} catch {
    Avisar "No se pudo descargar Autologon de Microsoft: $($_.Exception.Message)" "Error"
    Escribir-Registro "Autologon: error de descarga."
    exit 0
}
$firma = Get-AuthenticodeSignature $destino
if ($firma.Status -ne "Valid" -or $firma.SignerCertificate.Subject -notmatch "O=Microsoft Corporation") {
    Remove-Item $destino -Force
    Avisar "La firma de Autologon no es válida; no se ejecuta." "Error"
    Escribir-Registro "Autologon: firma no válida ($($firma.Status))."
    exit 0
}

Avisar ("Se abrirá Autologon de Microsoft. Escribe la contraseña de tu cuenta y pulsa «Enable».`n`n" +
        "Para desactivarlo en el futuro, vuelve a abrirlo y pulsa «Disable».")
Start-Process $destino -ArgumentList "-accepteula" -Wait
Escribir-Registro "Autologon: ejecutado."
