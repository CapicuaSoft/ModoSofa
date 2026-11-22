using System.Drawing.Drawing2D;

namespace ModoSofa.Agente.Interfaz;

/// <summary>Icono de la bandeja dibujado en código: un sofá sobre fondo violeta.</summary>
internal static class Icono
{
    public static Icon Crear()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fondo = new SolidBrush(Color.FromArgb(108, 92, 231));
            using (var ruta = Redondeado(new Rectangle(0, 0, 31, 31), 7)) g.FillPath(fondo, ruta);

            using var blanco = new SolidBrush(Color.White);
            g.FillRectangle(blanco, 9, 9, 14, 8);   // respaldo
            g.FillRectangle(blanco, 5, 14, 5, 9);   // brazo izquierdo
            g.FillRectangle(blanco, 22, 14, 5, 9);  // brazo derecho
            g.FillRectangle(blanco, 9, 18, 14, 5);  // asiento
            g.FillRectangle(blanco, 7, 23, 3, 3);   // patas
            g.FillRectangle(blanco, 22, 23, 3, 3);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var p = new GraphicsPath();
        var d = radio * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
