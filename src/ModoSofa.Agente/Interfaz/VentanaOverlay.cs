// ModoSofá - Convierte el PC de trabajo en una consola de salón.
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

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ModoSofa.Agente.Win32;
using ModoSofa.Nucleo;

namespace ModoSofa.Agente.Interfaz;

/// <summary>
/// Menú en la tele. Ventana sin bordes, siempre encima, que <b>no roba el foco</b>
/// (WS_EX_NOACTIVATE): el juego sigue siendo la ventana activa. El mando no llega por teclado
/// sino como mensajes que el agente envía a la página.
/// </summary>
internal sealed class VentanaOverlay : Form
{
    private readonly WebView2 _web;
    private readonly string _url;
    private readonly string _datos;
    private readonly Action _alCerrar;
    private bool _iniciado;

    public VentanaOverlay(int puerto, string token, string carpetaDatos, Action alCerrar)
    {
        _url = $"http://localhost:{puerto}/overlay.html#t={token}";
        _datos = carpetaDatos;
        _alCerrar = alCerrar;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(18, 20, 28);
        Text = "ModoSofá";

        _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = BackColor };
        Controls.Add(_web);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Nativo.WS_EX_NOACTIVATE | Nativo.WS_EX_TOOLWINDOW | Nativo.WS_EX_TOPMOST;
            return cp;
        }
    }

    public async void Mostrar()
    {
        // En modo sofá la tele es la pantalla principal.
        var area = Screen.PrimaryScreen!.Bounds;
        var ancho = (int)(area.Width * 0.62);
        var alto = (int)(area.Height * 0.78);
        Bounds = new Rectangle(area.X + (area.Width - ancho) / 2, area.Y + (area.Height - alto) / 2, ancho, alto);
        Show();
        Nativo.SetWindowPos(Handle, Nativo.HWND_TOPMOST, 0, 0, 0, 0,
            Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE | Nativo.SWP_SHOWWINDOW);

        if (!_iniciado)
        {
            _iniciado = true;
            var entorno = await CoreWebView2Environment.CreateAsync(null, _datos);
            await _web.EnsureCoreWebView2Async(entorno);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.TryGetWebMessageAsString() == "cerrar") _alCerrar();
            };
            _web.CoreWebView2.Navigate(_url);
        }
        else
        {
            _web.CoreWebView2?.PostWebMessageAsString("abierto");
        }
    }

    public void EnviarBoton(Botones boton)
    {
        if (Visible) _web.CoreWebView2?.PostWebMessageAsString("boton:" + boton);
    }
}
