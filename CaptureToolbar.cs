using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Qusasa;

internal sealed class CaptureToolbar : FlowLayoutPanel
{
    private static readonly Color Surface = Color.FromArgb(24, 35, 49);
    internal static readonly Color Accent = Color.FromArgb(99, 229, 219);

    private readonly ToolTip hints = new() { InitialDelay = 320 };
    private Image? appMark;
    private Point? dragStart;
    internal event Action<Point>? MoveRequested;
    private readonly Font systemIconFont = new("Segoe MDL2 Assets", 12, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font iconFont = new("Segoe UI Symbol", 13, FontStyle.Regular, GraphicsUnit.Point);

    internal CaptureToolbar()
    {
        BackColor = Surface;
        Padding = new Padding(8, 6, 8, 6);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = false;
        RightToLeft = !Ui.IsArabic ? RightToLeft.No : RightToLeft.Yes;
    }

    internal Button Button(string ar, string en, Action click, bool primary = false)
    {
        var button = new Button
        {
            Text = Display(ar, en),
            AutoSize = true,
            MinimumSize = new Size(58, 31),
            Padding = new Padding(5, 0, 5, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.FromArgb(39, 54, 69),
            ForeColor = primary ? Surface : Color.White,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, __) => click();
        Controls.Add(button);
        return button;
    }

    internal void AddAppMark()
    {
        var source = (System.Windows.Media.Imaging.BitmapSource)System.Windows.Application.Current.FindResource("AppMarkImage");
        appMark = ThemeIcon.CopyBitmap(source);
        var logo = new PictureBox
        {
            Cursor = Cursors.SizeAll,
            Size = new Size(30, 30),
            Margin = new Padding(4, 3, 7, 3),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = appMark,
            AccessibleName = Display("قصاصة", "Qusasa")
        };
        Controls.Add(logo);
        hints.SetToolTip(logo, Display("اسحب لنقل شريط الأدوات", "Drag to move the toolbar"));
        AddDragHandle(logo);
        AddDragHandle(this);
    }

    private void AddDragHandle(Control handle)
    {
        handle.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            var cursor = Parent.PointToClient(handle.PointToScreen(e.Location));
            dragStart = new Point(cursor.X - Left, cursor.Y - Top);
            handle.Capture = true;
        };
        handle.MouseMove += (_, e) =>
        {
            if (!handle.Capture || dragStart is not Point offset) return;
            var cursor = Parent.PointToClient(handle.PointToScreen(e.Location));
            MoveRequested?.Invoke(new Point(cursor.X - offset.X, cursor.Y - offset.Y));
        };
        handle.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dragStart = null;
            handle.Capture = false;
        };
        handle.MouseCaptureChanged += (_, __) =>
        {
            if (!handle.Capture) dragStart = null;
        };
    }

    private static GraphicsPath RoundedPath(Size size, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(size.Width, size.Height));
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(size.Width - d, 0, d, d, 270, 90);
        path.AddArc(size.Width - d, size.Height - d, d, d, 0, 90);
        path.AddArc(0, size.Height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Shape(this, 10);
    }

    private static void Shape(Control control, int radius)
    {
        if (control.Width <= 1 || control.Height <= 1) return;
        using var path = RoundedPath(control.Size, radius);
        var previous = control.Region;
        control.Region = new Region(path);
        previous?.Dispose();
    }

    internal Button IconButton(string glyph, string ar, string en, Action click, bool primary = false)
    {
        var button = Button(ar, en, click, primary);
        button.AccessibleName = button.Text;
        hints.SetToolTip(button, button.Text);
        button.AutoSize = false;
        button.MinimumSize = Size.Empty;
        button.Padding = Padding.Empty;
        button.Margin = new Padding(2);
        button.Size = new Size(34, 32);
        button.Font = glyph.Length == 1 && glyph[0] >= '\ue000' && glyph[0] <= '\uf8ff' ? systemIconFont : iconFont;
        button.Text = glyph;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 82, 103);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(54, 72, 92);
        Shape(button, 7);
        return button;
    }

    internal static Point PlaceNear(Rectangle selection, Rectangle usable, Size toolbar, bool rightToLeft)
    {
        const int gap = 8;
        int x = rightToLeft ? selection.Right - toolbar.Width : selection.Left;
        x = Math.Max(usable.Left + gap, Math.Min(x, usable.Right - toolbar.Width - gap));
        int below = selection.Bottom + gap;
        int above = selection.Top - toolbar.Height - gap;
        int bottomLimit = usable.Bottom - toolbar.Height - gap;
        int y = below <= bottomLimit ? below : above >= usable.Top + gap ? above : bottomLimit;
        return new Point(x, Math.Max(usable.Top, y));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        hints.Dispose();
        appMark?.Dispose();
        iconFont.Dispose();
        systemIconFont.Dispose();
    }

    internal static void SetActive(Button button, bool active)
    {
        button.BackColor = active ? Accent : Color.FromArgb(39, 54, 69);
        button.ForeColor = active ? Surface : Color.White;
    }

    internal void CenterIn(Control parent, int y) => Location = new Point(Math.Max(0, (parent.ClientSize.Width - Width) / 2), y);
    internal static string Display(string ar, string en) => Ui.IsArabic ? ar : Ui.T(ar, en);
}
