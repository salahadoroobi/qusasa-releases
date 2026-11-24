using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using GradientBrush = System.Windows.Media.GradientBrush;

namespace Qusasa;

internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    internal TrayMenuRenderer() : base(new TrayColors()) { RoundedEdges = false; }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed) return;
        var bounds = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        using var fill = new SolidBrush(ColorOf("Tint"));
        using var outline = new Pen(ColorOf("Accent"));
        e.Graphics.FillRectangle(fill, bounds);
        e.Graphics.DrawRectangle(outline, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = ColorOf(e.Item.Enabled ? "Text" : "Muted");
        base.OnRenderItemText(e);
    }

    internal static void SetGlyph(ToolStripMenuItem item, string glyph)
    {
        item.Tag = glyph;
        var bitmap = new Bitmap(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Segoe MDL2 Assets", 14, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(ColorOf("Accent")))
        using (var layout = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.DrawString(glyph, font, brush, new RectangleF(0, 0, bitmap.Width, bitmap.Height), layout);
        }
        var previous = item.Image;
        item.Image = bitmap;
        previous?.Dispose();
    }

    private static Color ColorOf(string resource)
    {
        var brush = System.Windows.Application.Current.TryFindResource(resource);
        var color = brush switch
        {
            SolidColorBrush solid => solid.Color,
            GradientBrush gradient when gradient.GradientStops.Count > 0 =>
                gradient.GradientStops[gradient.GradientStops.Count / 2].Color,
            _ => System.Windows.Media.Colors.Transparent
        };
        if (color.A == 0) return SystemColors.Menu;
        return Color.FromArgb(color.R, color.G, color.B);
    }

    private sealed class TrayColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ColorOf("Panel");
        public override Color MenuBorder => ColorOf("Line");
        public override Color MenuItemBorder => ColorOf("Line");
        public override Color MenuItemSelected => ColorOf("Hover");
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color SeparatorDark => ColorOf("Line");
        public override Color SeparatorLight => ColorOf("Line");
    }
}
