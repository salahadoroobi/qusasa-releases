using System;
using System.Drawing;
using System.Windows.Forms;

namespace Qusasa;

internal sealed class CaptureColorWheel : ToolStripDropDown
{
    private const int Diameter = 154;
    private readonly Bitmap wheel = new(Diameter, Diameter);
    private readonly TrackBar brightness;
    private Color chroma;

    internal CaptureColorWheel(Color initial, Action<Color> choose)
    {
        var picker = new Panel { Size = new Size(174, 218), BackColor = Color.FromArgb(24, 35, 49) };
        chroma = initial;
        for (int y = 0; y < Diameter; y++)
            for (int x = 0; x < Diameter; x++)
            {
                double dx = x + .5 - Diameter / 2d, dy = y + .5 - Diameter / 2d;
                double saturation = Math.Sqrt(dx * dx + dy * dy) / (Diameter / 2d);
                if (saturation <= 1) wheel.SetPixel(x, y, HueColor(Math.Atan2(dy, dx) / (Math.PI * 2), saturation));
            }
        var image = new PictureBox
        {
            Location = new Point(10, 4), Size = wheel.Size, Image = wheel, Cursor = Cursors.Cross,
            AccessibleName = CaptureToolbar.Display("عجلة الألوان", "Color wheel")
        };
        brightness = new TrackBar
        {
            Location = new Point(6, 159), Width = 162, Height = 24, AutoSize = false,
            Minimum = 0, Maximum = 100, Value = 100, TickStyle = TickStyle.None,
            AccessibleName = CaptureToolbar.Display("السطوع", "Brightness")
        };
        var apply = new Button
        {
            Text = CaptureToolbar.Display("اعتماد اللون", "Apply color"),
            Location = new Point(10, 184), Size = new Size(154, 27), FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White, BackColor = initial
        };
        image.MouseDown += (_, mouse) =>
        {
            if (!new Rectangle(Point.Empty, wheel.Size).Contains(mouse.Location)) return;
            var pixel = wheel.GetPixel(mouse.X, mouse.Y);
            if (pixel.A == 0) return;
            chroma = pixel;
            UpdatePreview();
        };
        brightness.ValueChanged += (_, __) => UpdatePreview();
        apply.Click += (_, __) => { choose(CurrentColor()); Close(); };
        picker.Controls.AddRange(new Control[] { image, brightness, apply });
        Items.Add(new ToolStripControlHost(picker) { Margin = Padding.Empty, Padding = Padding.Empty });
        Closed += (_, __) => Dispose();
        UpdatePreview();

        void UpdatePreview()
        {
            var color = CurrentColor();
            apply.BackColor = color;
            apply.ForeColor = color.GetBrightness() > .54 ? Color.Black : Color.White;
        }
    }

    private Color CurrentColor() => Color.FromArgb(chroma.R * brightness.Value / 100,
        chroma.G * brightness.Value / 100, chroma.B * brightness.Value / 100);

    internal static Color HueColor(double hue, double saturation)
    {
        double h = ((hue % 1 + 1) % 1) * 6;
        double baseColor = 1 - saturation;
        double middle = baseColor + saturation * (1 - Math.Abs(h % 2 - 1));
        var channels = (int)h switch
        {
            0 => (1d, middle, baseColor), 1 => (middle, 1d, baseColor),
            2 => (baseColor, 1d, middle), 3 => (baseColor, middle, 1d),
            4 => (middle, baseColor, 1d), _ => (1d, baseColor, middle)
        };
        return Color.FromArgb((int)(channels.Item1 * 255), (int)(channels.Item2 * 255), (int)(channels.Item3 * 255));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) wheel.Dispose();
    }
}
