// ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
// Copyright (C) 2026  José Luis Bautista Martín
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

/// <summary>QR con la dirección del panel y el token: se escanea con la cámara del móvil.</summary>
internal sealed class VentanaVincular : Form
{
    public VentanaVincular(int puerto, string token)
    {
        Text = "Vincular móvil — ModoSofá";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 600);
        BackColor = Color.White;

        var direcciones = DireccionesLocales().ToList();
        var ip = direcciones.FirstOrDefault() ?? "localhost";
        var url = $"http://{ip}:{puerto}/#t={token}";

        var qr = new PictureBox
        {
            Image = GenerarQr(url, 10),
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(20, 20, 400, 400),
        };

        var texto = new Label
        {
            Bounds = new Rectangle(20, 430, 400, 110),
            Font = new Font("Segoe UI", 9.5f),
            Text = "Escanea el código con la cámara del móvil (misma red Wi-Fi) y añade la página a la " +
                   "pantalla de inicio.\n\nFuera de casa: instala Tailscale en el PC y en el móvil y usa la " +
                   "dirección de Tailscale (ver docs/INSTALACION.md).\n\n" +
                   (direcciones.Count > 1 ? $"Otras IP de este PC: {string.Join(", ", direcciones.Skip(1))}" : ""),
        };

        var copiar = new Button { Text = "Copiar enlace", Bounds = new Rectangle(20, 550, 190, 32) };
        copiar.Click += (_, _) => Clipboard.SetText(url);
        var cerrar = new Button { Text = "Cerrar", Bounds = new Rectangle(230, 550, 190, 32) };
        cerrar.Click += (_, _) => Close();

        Controls.AddRange([qr, texto, copiar, cerrar]);
    }

    private static Bitmap GenerarQr(string texto, int escala)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        var matriz = datos.ModuleMatrix;
        var lado = matriz.Count;
        var bmp = new Bitmap(lado * escala, lado * escala);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        for (var y = 0; y < lado; y++)
            for (var x = 0; x < lado; x++)
                if (matriz[y][x])
                    g.FillRectangle(Brushes.Black, x * escala, y * escala, escala, escala);
        return bmp;
    }

    /// <summary>IPv4 de las interfaces activas, primero las de red doméstica habitual.</summary>
    private static IEnumerable<string> DireccionesLocales() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .OrderBy(ip => ip.StartsWith("192.168.") ? 0 : ip.StartsWith("10.") ? 1 : ip.StartsWith("100.") ? 3 : 2);
}
