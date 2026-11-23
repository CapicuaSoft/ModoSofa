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

using System.Drawing.Drawing2D;
using Microsoft.Win32;
using CapicuaSoft.ModoSofa.Agente.Win32;

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

/// <summary>
/// Pantalla de espera mientras se entra en modo sofá: Steam tarda en arrancar (~15 s solo en
/// lanzar steam.exe) y, sin ella, parece que el PC no hace nada. Ocupa la pantalla principal (la
/// tele, en cuanto se cambia) y se recoloca cuando cambia la topología. Siempre encima pero
/// <b>sin tomar el foco</b>, para no quitárselo a Big Picture; un clic o el tiempo máximo la
/// cierran por si algo se queda colgado.
/// </summary>
internal sealed class VentanaEspera : Form
{
    private static readonly Color Fondo = Color.FromArgb(18, 20, 28);
    private static readonly Color Violeta = Color.FromArgb(124, 108, 242);
    private static readonly Color Pista = Color.FromArgb(40, 44, 60);
    private static readonly Color TextoSuave = Color.FromArgb(160, 166, 184);
    private static readonly TimeSpan Maximo = TimeSpan.FromMinutes(3);

    private readonly System.Windows.Forms.Timer _animacion = new() { Interval = 33 };
    private string _paso = "";
    private DateTime _desde;
    private float _angulo;

    public VentanaEspera()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Fondo;
        Text = "ModoSofá";
        Cursor = Cursors.WaitCursor;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        _animacion.Tick += (_, _) =>
        {
            if (DateTime.UtcNow - _desde > Maximo) { Ocultar(); return; }
            _angulo = (_angulo + 9) % 360;
            Recolocar();
            Invalidate();
        };
        Click += (_, _) => Ocultar();
        SystemEvents.DisplaySettingsChanged += AlCambiarPantallas;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Fuera del Alt+Tab, de la barra de tareas y del guardián; nunca activa.
            cp.ExStyle |= Nativo.WS_EX_TOOLWINDOW | Nativo.WS_EX_TOPMOST | Nativo.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>Muestra la pantalla (si no lo estaba) con el paso en curso.</summary>
    public void Mostrar(string paso)
    {
        _paso = paso;
        if (!Visible)
        {
            _desde = DateTime.UtcNow;
            Recolocar();
            Show();
            _animacion.Start();
        }
        Invalidate();
    }

    public void Ocultar()
    {
        _animacion.Stop();
        Hide();
    }

    private void AlCambiarPantallas(object? sender, EventArgs e)
    {
        if (IsHandleCreated) BeginInvoke(Recolocar);
    }

    private void Recolocar()
    {
        var area = Screen.PrimaryScreen?.Bounds ?? Bounds;
        if (Bounds != area) Bounds = area;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Fondo);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var unidad = Math.Max(1f, h / 100f);
        var cx = w / 2f;
        var cy = h * 0.40f;

        // Indicador giratorio: pista tenue y arco violeta.
        var radio = unidad * 7;
        var grosor = unidad * 1.1f;
        var circulo = new RectangleF(cx - radio, cy - radio, radio * 2, radio * 2);
        using (var pista = new Pen(Pista, grosor))
            g.DrawEllipse(pista, circulo);
        using (var arco = new Pen(Violeta, grosor) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arco, circulo, _angulo, 100);

        var centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        using var titulo = new Font("Segoe UI Semibold", unidad * 5.5f, GraphicsUnit.Pixel);
        using var paso = new Font("Segoe UI", unidad * 2.8f, GraphicsUnit.Pixel);
        using var pie = new Font("Segoe UI", unidad * 2f, GraphicsUnit.Pixel);

        var y = cy + radio + unidad * 6;
        g.DrawString("Entrando en modo sofá", titulo, Brushes.White, new RectangleF(0, y, w, unidad * 9), centrado);
        y += unidad * 9;
        using (var suave = new SolidBrush(TextoSuave))
        {
            g.DrawString(_paso, paso, suave, new RectangleF(w * 0.1f, y, w * 0.8f, unidad * 8), centrado);
            var segundos = (int)(DateTime.UtcNow - _desde).TotalSeconds;
            g.DrawString($"{segundos} s", pie, suave, new RectangleF(0, h - unidad * 8, w, unidad * 4), centrado);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= AlCambiarPantallas;
            _animacion.Dispose();
        }
        base.Dispose(disposing);
    }
}
