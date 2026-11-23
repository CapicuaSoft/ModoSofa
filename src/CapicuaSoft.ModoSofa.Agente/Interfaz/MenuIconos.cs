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

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

/// <summary>
/// Iconos del menú de la bandeja dibujados con GDI+, con el mismo método y la misma paleta que
/// SilentRunner (los que coinciden —información, corazón, carpeta, documento, salir— son el mismo
/// dibujo). Los menús de Windows Forms pintan el texto con GDI, que muestra los emoji en monocromo:
/// por eso los iconos van como imagen. Se generan una vez y se reutilizan; nunca se liberan
/// mientras el icono de la bandeja siga vivo.
/// </summary>
internal static class MenuIconos
{
    /// <summary>Lado del lienzo. El menú lo reescala al tamaño que toque según el DPI.</summary>
    private const int Lado = 32;

    // ── Paleta (la de SilentRunner + el violeta de ModoSofá) ─────────────────
    private static readonly Color Verde = Color.FromArgb(46, 158, 75);
    private static readonly Color Rojo = Color.FromArgb(200, 80, 63);
    private static readonly Color Pizarra = Color.FromArgb(90, 100, 112);
    private static readonly Color Ambar = Color.FromArgb(227, 165, 60);
    private static readonly Color Dorado = Color.FromArgb(255, 203, 76);
    private static readonly Color DoradoBorde = Color.FromArgb(214, 158, 46);
    private static readonly Color Papel = Color.FromArgb(238, 243, 248);
    private static readonly Color Violeta = Color.FromArgb(124, 108, 242);
    private static readonly Color Azul = Color.FromArgb(52, 120, 196);

    // ── Iconos (generación diferida y cacheada) ───────────────────────────────
    private static Image? _sofa, _trabajo, _tele, _detener, _movil, _globo, _llave;
    private static Image? _carpeta, _documento, _actualizar, _informacion, _corazon, _salir;

    internal static Image Sofa => _sofa ??= Crear(DibujarSofa);
    internal static Image Trabajo => _trabajo ??= Crear(DibujarTrabajo);
    internal static Image Tele => _tele ??= Crear(DibujarTele);
    internal static Image Detener => _detener ??= Crear(DibujarDetener);
    internal static Image Movil => _movil ??= Crear(DibujarMovil);
    internal static Image Globo => _globo ??= Crear(DibujarGlobo);
    internal static Image Llave => _llave ??= Crear(DibujarLlave);
    internal static Image Carpeta => _carpeta ??= Crear(DibujarCarpeta);
    internal static Image Documento => _documento ??= Crear(DibujarDocumento);
    internal static Image Actualizar => _actualizar ??= Crear(DibujarActualizar);
    internal static Image Informacion => _informacion ??= Crear(DibujarInformacion);
    internal static Image Corazon => _corazon ??= Crear(DibujarCorazon);
    internal static Image Salir => _salir ??= Crear(DibujarSalir);

    // ── Infraestructura de dibujo ─────────────────────────────────────────────

    private static Image Crear(Action<Graphics, float> dibujar)
    {
        var bmp = new Bitmap(Lado, Lado);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        dibujar(g, Lado);
        return bmp;
    }

    /// <summary>Convierte coordenadas normalizadas (0..1) a píxeles del lienzo.</summary>
    private static PointF P(float s, float x, float y) => new(x * s, y * s);

    private static RectangleF R(float s, float x, float y, float ancho, float alto) =>
        new(x * s, y * s, ancho * s, alto * s);

    // ── Propios de ModoSofá ───────────────────────────────────────────────────

    private static void DibujarSofa(Graphics g, float s)
    {
        // El mismo sofá del icono de la bandeja, en violeta.
        using var brocha = new SolidBrush(Violeta);
        using (var respaldo = RectanguloRedondeado(R(s, 0.26f, 0.24f, 0.48f, 0.26f), s * 0.05f)) g.FillPath(brocha, respaldo);
        using (var izquierdo = RectanguloRedondeado(R(s, 0.12f, 0.40f, 0.17f, 0.32f), s * 0.05f)) g.FillPath(brocha, izquierdo);
        using (var derecho = RectanguloRedondeado(R(s, 0.71f, 0.40f, 0.17f, 0.32f), s * 0.05f)) g.FillPath(brocha, derecho);
        g.FillRectangle(brocha, R(s, 0.26f, 0.54f, 0.48f, 0.18f));
        g.FillRectangle(brocha, R(s, 0.17f, 0.72f, 0.08f, 0.10f));
        g.FillRectangle(brocha, R(s, 0.75f, 0.72f, 0.08f, 0.10f));
    }

    private static void DibujarTrabajo(Graphics g, float s)
    {
        // Monitor de escritorio con ventanas: volver al modo trabajo.
        using var marco = RectanguloRedondeado(R(s, 0.10f, 0.16f, 0.80f, 0.54f), s * 0.06f);
        using var pizarra = new SolidBrush(Pizarra);
        g.FillPath(pizarra, marco);
        using var pantalla = new SolidBrush(Papel);
        g.FillRectangle(pantalla, R(s, 0.16f, 0.22f, 0.68f, 0.42f));
        using var ventana = new SolidBrush(Azul);
        g.FillRectangle(ventana, R(s, 0.22f, 0.28f, 0.26f, 0.16f));
        g.FillRectangle(ventana, R(s, 0.52f, 0.40f, 0.26f, 0.18f));
        using var pie = new Pen(Pizarra, s * 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pie, P(s, 0.50f, 0.70f), P(s, 0.50f, 0.82f));
        g.DrawLine(pie, P(s, 0.34f, 0.84f), P(s, 0.66f, 0.84f));
    }

    private static void DibujarTele(Graphics g, float s)
    {
        // Tele con el menú dentro (tres renglones, el primero seleccionado).
        using var marco = RectanguloRedondeado(R(s, 0.08f, 0.18f, 0.84f, 0.54f), s * 0.06f);
        using var oscuro = new SolidBrush(Color.FromArgb(40, 44, 58));
        g.FillPath(oscuro, marco);
        using var sel = new SolidBrush(Violeta);
        using var renglon = new SolidBrush(Color.FromArgb(150, 160, 175));
        g.FillRectangle(sel, R(s, 0.20f, 0.28f, 0.60f, 0.09f));
        g.FillRectangle(renglon, R(s, 0.20f, 0.42f, 0.60f, 0.07f));
        g.FillRectangle(renglon, R(s, 0.20f, 0.54f, 0.60f, 0.07f));
        using var pata = new Pen(Pizarra, s * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pata, P(s, 0.30f, 0.72f), P(s, 0.24f, 0.84f));
        g.DrawLine(pata, P(s, 0.70f, 0.72f), P(s, 0.76f, 0.84f));
    }

    private static void DibujarDetener(Graphics g, float s)
    {
        using var trazado = RectanguloRedondeado(R(s, 0.24f, 0.24f, 0.52f, 0.52f), s * 0.08f);
        using var brocha = new SolidBrush(Rojo);
        g.FillPath(brocha, trazado);
    }

    private static void DibujarMovil(Graphics g, float s)
    {
        using var cuerpo = RectanguloRedondeado(R(s, 0.28f, 0.08f, 0.44f, 0.84f), s * 0.08f);
        using var pizarra = new SolidBrush(Pizarra);
        g.FillPath(pizarra, cuerpo);
        using var pantalla = new SolidBrush(Papel);
        g.FillRectangle(pantalla, R(s, 0.33f, 0.17f, 0.34f, 0.60f));
        // Un QR esquemático: vincular por código.
        using var qr = new SolidBrush(Violeta);
        g.FillRectangle(qr, R(s, 0.38f, 0.30f, 0.10f, 0.10f));
        g.FillRectangle(qr, R(s, 0.52f, 0.30f, 0.10f, 0.10f));
        g.FillRectangle(qr, R(s, 0.38f, 0.46f, 0.10f, 0.10f));
        g.FillRectangle(qr, R(s, 0.54f, 0.48f, 0.06f, 0.06f));
        g.FillEllipse(pantalla, R(s, 0.46f, 0.81f, 0.08f, 0.06f));
    }

    private static void DibujarGlobo(Graphics g, float s)
    {
        using var lapiz = new Pen(Azul, s * 0.07f);
        var circulo = R(s, 0.14f, 0.14f, 0.72f, 0.72f);
        g.DrawEllipse(lapiz, circulo);
        g.DrawEllipse(lapiz, R(s, 0.34f, 0.14f, 0.32f, 0.72f));
        g.DrawLine(lapiz, P(s, 0.14f, 0.50f), P(s, 0.86f, 0.50f));
        g.DrawLine(lapiz, P(s, 0.50f, 0.14f), P(s, 0.50f, 0.86f));
    }

    private static void DibujarLlave(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Ambar);
        using var lapiz = new Pen(Ambar, s * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillEllipse(brocha, R(s, 0.10f, 0.30f, 0.36f, 0.36f));
        using var agujero = new SolidBrush(Papel);
        g.FillEllipse(agujero, R(s, 0.20f, 0.42f, 0.12f, 0.12f));
        g.DrawLine(lapiz, P(s, 0.44f, 0.48f), P(s, 0.86f, 0.48f));
        g.DrawLine(lapiz, P(s, 0.74f, 0.48f), P(s, 0.74f, 0.64f));
        g.DrawLine(lapiz, P(s, 0.86f, 0.48f), P(s, 0.86f, 0.60f));
    }

    private static void DibujarActualizar(Graphics g, float s)
    {
        // Flecha de descarga sobre una bandeja: hay versión nueva.
        using var lapiz = new Pen(Verde, s * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(lapiz, P(s, 0.50f, 0.12f), P(s, 0.50f, 0.58f));
        g.DrawLines(lapiz, [P(s, 0.30f, 0.40f), P(s, 0.50f, 0.60f), P(s, 0.70f, 0.40f)]);
        g.DrawLines(lapiz, [P(s, 0.16f, 0.62f), P(s, 0.16f, 0.84f), P(s, 0.84f, 0.84f), P(s, 0.84f, 0.62f)]);
    }

    // ── Compartidos con SilentRunner (mismo dibujo) ───────────────────────────

    private static void DibujarCarpeta(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Ambar);
        using var pestana = RectanguloRedondeado(R(s, 0.12f, 0.20f, 0.34f, 0.18f), s * 0.05f);
        g.FillPath(brocha, pestana);
        using var cuerpo = RectanguloRedondeado(R(s, 0.12f, 0.28f, 0.76f, 0.52f), s * 0.07f);
        g.FillPath(brocha, cuerpo);
        using var bisel = new Pen(Color.FromArgb(120, 255, 255, 255), s * 0.06f);
        g.DrawLine(bisel, P(s, 0.16f, 0.44f), P(s, 0.84f, 0.44f));
    }

    private static void DibujarDocumento(Graphics g, float s)
    {
        PointF[] hoja =
        [
            P(s, 0.24f, 0.12f), P(s, 0.60f, 0.12f), P(s, 0.78f, 0.30f),
            P(s, 0.78f, 0.88f), P(s, 0.24f, 0.88f),
        ];
        using var papel = new SolidBrush(Papel);
        using var borde = new Pen(Pizarra, s * 0.09f) { LineJoin = LineJoin.Round };
        g.FillPolygon(papel, hoja);
        g.DrawPolygon(borde, hoja);
        using var pliegue = new Pen(Pizarra, s * 0.06f);
        g.DrawLines(pliegue, [P(s, 0.60f, 0.12f), P(s, 0.60f, 0.30f), P(s, 0.78f, 0.30f)]);
        using var renglon = new Pen(Pizarra, s * 0.07f);
        g.DrawLine(renglon, P(s, 0.35f, 0.52f), P(s, 0.67f, 0.52f));
        g.DrawLine(renglon, P(s, 0.35f, 0.68f), P(s, 0.67f, 0.68f));
    }

    private static void DibujarInformacion(Graphics g, float s)
    {
        using var disco = new SolidBrush(Pizarra);
        g.FillEllipse(disco, R(s, 0.14f, 0.14f, 0.72f, 0.72f));
        using var tinta = new SolidBrush(Papel);
        g.FillEllipse(tinta, R(s, 0.44f, 0.26f, 0.12f, 0.12f));
        using var palo = new Pen(Papel, s * 0.12f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(palo, P(s, 0.50f, 0.45f), P(s, 0.50f, 0.72f));
    }

    private static void DibujarCorazon(Graphics g, float s)
    {
        using var trazado = new GraphicsPath();
        trazado.AddBezier(P(s, 0.50f, 0.94f), P(s, 0.10f, 0.62f), P(s, 0.02f, 0.38f), P(s, 0.17f, 0.22f));
        trazado.AddBezier(P(s, 0.17f, 0.22f), P(s, 0.30f, 0.09f), P(s, 0.46f, 0.13f), P(s, 0.50f, 0.26f));
        trazado.AddBezier(P(s, 0.50f, 0.26f), P(s, 0.54f, 0.13f), P(s, 0.70f, 0.09f), P(s, 0.83f, 0.22f));
        trazado.AddBezier(P(s, 0.83f, 0.22f), P(s, 0.98f, 0.38f), P(s, 0.90f, 0.62f), P(s, 0.50f, 0.94f));
        trazado.CloseFigure();
        using var relleno = new SolidBrush(Dorado);
        using var borde = new Pen(DoradoBorde, s / 28f);
        g.FillPath(relleno, trazado);
        g.DrawPath(borde, trazado);
    }

    private static void DibujarSalir(Graphics g, float s)
    {
        using var lapiz = new Pen(Pizarra, s * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(lapiz, R(s, 0.20f, 0.20f, 0.60f, 0.60f), -60f, 300f);
        g.DrawLine(lapiz, P(s, 0.50f, 0.12f), P(s, 0.50f, 0.46f));
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    private static GraphicsPath RectanguloRedondeado(RectangleF r, float radio)
    {
        var d = radio * 2;
        var trazado = new GraphicsPath();
        trazado.AddArc(r.Left, r.Top, d, d, 180, 90);
        trazado.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        trazado.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        trazado.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        trazado.CloseFigure();
        return trazado;
    }
}
