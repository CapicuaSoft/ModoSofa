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

namespace CapicuaSoft.ModoSofa.Agente.Interfaz;

/// <summary>
/// Dibuja en un PNG un menú con los mismos iconos y textos que el de la bandeja, sin abrirlo.
/// Sirve para revisar los iconos sin tener que hacer clic derecho (<c>--vista-menu salida.png</c>).
/// </summary>
internal static class VistaMenu
{
    public static void Guardar(string ruta)
    {
        Application.EnableVisualStyles();
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            new ToolStripMenuItem("ModoSofá — modo trabajo", MenuIconos.Trabajo) { Enabled = false },
            new ToolStripSeparator(),
            new ToolStripMenuItem("Activar modo sofá", MenuIconos.Sofa),
            new ToolStripMenuItem("Mostrar menú en la tele", MenuIconos.Tele),
            new ToolStripMenuItem("Cerrar juego", MenuIconos.Detener),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Vincular móvil…", MenuIconos.Movil),
            new ToolStripMenuItem("Abrir panel en el navegador", MenuIconos.Globo),
            new ToolStripMenuItem("Copiar token de Alexa", MenuIconos.Llave),
            new ToolStripMenuItem("Abrir carpeta de configuración", MenuIconos.Carpeta),
            new ToolStripMenuItem("Abrir carpeta de logs", MenuIconos.Documento),
            new ToolStripMenuItem("Iniciar con Windows") { Checked = true },
            new ToolStripSeparator(),
            new ToolStripMenuItem("Hay una versión nueva: 0.3.0", MenuIconos.Actualizar) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) },
            new ToolStripMenuItem("Acerca de ModoSofá", MenuIconos.Informacion),
            new ToolStripMenuItem("Invitar a un café", MenuIconos.Corazon),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Salir", MenuIconos.Salir),
        ]);
        menu.CreateControl();
        menu.PerformLayout();
        var tamano = menu.GetPreferredSize(Size.Empty);
        menu.Size = tamano;
        using var bmp = new Bitmap(tamano.Width, tamano.Height);
        menu.DrawToBitmap(bmp, new Rectangle(Point.Empty, tamano));
        bmp.Save(ruta);
    }
}
