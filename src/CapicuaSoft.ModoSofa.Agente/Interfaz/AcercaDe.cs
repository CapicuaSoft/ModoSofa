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

using System.Diagnostics;
using CapicuaSoft.ModoSofa.Agente.Servicios;

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

/// <summary>Enlaces que salen del programa: el menú de la bandeja, «Acerca de» y la web los usan igual.</summary>
internal static class Enlaces
{
    public const string Proyecto = "https://github.com/CapicuaSoft/ModoSofa";
    public const string Donacion = "https://paypal.me/jbautistamartin";
    public const string Licencia = "https://github.com/CapicuaSoft/ModoSofa/blob/main/LICENSE";

    public static void Abrir(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sin navegador predeterminado: no hay nada que hacer */ }
    }
}

/// <summary>«Acerca de»: versión, proyecto, actualización, donación y licencia. Una sola a la vez.</summary>
internal sealed class AcercaDe : Form
{
    private const int Ancho = 420;
    private static AcercaDe? _abierta;

    private readonly ActualizacionServicio _actualizacion;
    private readonly Label _situacion;
    private readonly Button _buscar, _actualizar;

    public static void Mostrar(ActualizacionServicio actualizacion)
    {
        if (_abierta is { IsDisposed: false })
        {
            _abierta.Activate();
            return;
        }
        _abierta = new AcercaDe(actualizacion);
        _abierta.Show();
    }

    private AcercaDe(ActualizacionServicio actualizacion)
    {
        _actualizacion = actualizacion;
        Text = "Acerca de ModoSofá";
        Icon = Icono.Crear();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        var caja = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(24, 20, 24, 20),
        };

        caja.Controls.Add(new Label { Text = "ModoSofá", Font = new Font("Segoe UI Semibold", 18f), AutoSize = true });
        caja.Controls.Add(Tenue($"Versión {PanelEstado.Version} · CapicuaSoft"));
        caja.Controls.Add(Parrafo("Convierte el PC de trabajo en una consola de salón al enchufar la tele, y lo deja como estaba al desenchufarla."));

        var proyecto = new LinkLabel { Text = Enlaces.Proyecto, AutoSize = true, Margin = new Padding(3, 4, 3, 12) };
        proyecto.LinkClicked += (_, _) => Enlaces.Abrir(Enlaces.Proyecto);
        caja.Controls.Add(proyecto);

        caja.Controls.Add(Raya());
        caja.Controls.Add(new Label { Text = "Actualizaciones", Font = new Font("Segoe UI Semibold", 10.5f), AutoSize = true, Margin = new Padding(3, 10, 3, 2) });
        _situacion = Parrafo(actualizacion.Situacion);
        caja.Controls.Add(_situacion);
        _buscar = Boton("Buscar ahora");
        _buscar.Click += async (_, _) => await Ejecutar(() => _actualizacion.BuscarAsync(forzar: true));
        _actualizar = Boton("Actualizar ahora");
        _actualizar.Click += async (_, _) => await Ejecutar(() => _actualizacion.InstalarAhoraAsync());
        var fila = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 0, 12) };
        fila.Controls.AddRange([_buscar, _actualizar]);
        caja.Controls.Add(fila);

        caja.Controls.Add(Raya());
        caja.Controls.Add(Parrafo("ModoSofá es gratuito. Si te resulta útil y quieres apoyar su desarrollo, puedes invitarme a un café. Es completamente opcional."));
        var cafe = Boton("☕  Invitar a un café");
        cafe.BackColor = Color.FromArgb(124, 108, 242);
        cafe.ForeColor = Color.White;
        cafe.FlatStyle = FlatStyle.Flat;
        cafe.FlatAppearance.BorderSize = 0;
        cafe.Click += (_, _) => Enlaces.Abrir(Enlaces.Donacion);
        caja.Controls.Add(cafe);

        var licencia = new LinkLabel
        {
            Text = "© 2026 José Luis Bautista Martín · CapicuaSoft · Distribuido bajo la licencia LGPL-2.1",
            AutoSize = true,
            MaximumSize = new Size(Ancho, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 16, 3, 0),
        };
        licencia.LinkArea = new LinkArea(licencia.Text.IndexOf("LGPL"), "LGPL-2.1".Length);
        licencia.LinkClicked += (_, _) => Enlaces.Abrir(Enlaces.Licencia);
        caja.Controls.Add(licencia);

        Controls.Add(caja);
        Refrescar();
        _actualizacion.Cambio += AlCambiar;
        FormClosed += (_, _) => _actualizacion.Cambio -= AlCambiar;
    }

    private void AlCambiar()
    {
        if (IsHandleCreated) BeginInvoke(Refrescar);
    }

    private void Refrescar()
    {
        _situacion.Text = _actualizacion.Situacion;
        _actualizar.Visible = _actualizacion.Disponible is not null;
        if (_actualizacion.Disponible is { } d) _actualizar.Text = $"Actualizar a la {d.Version}";
    }

    private async Task Ejecutar(Func<Task> accion)
    {
        _buscar.Enabled = _actualizar.Enabled = false;
        try { await Task.Run(accion); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "ModoSofá", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        finally
        {
            if (!IsDisposed) { _buscar.Enabled = _actualizar.Enabled = true; Refrescar(); }
        }
    }

    private static Label Parrafo(string texto) => new()
    {
        Text = texto, AutoSize = true, MaximumSize = new Size(Ancho, 0), Margin = new Padding(3, 4, 3, 4),
    };

    private static Label Tenue(string texto) => new() { Text = texto, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 8) };

    private static Button Boton(string texto) => new() { Text = texto, AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(3) };

    private static Control Raya() => new Panel { Height = 1, Width = Ancho, BackColor = Color.Gainsboro, Margin = new Padding(3, 6, 3, 6) };
}
