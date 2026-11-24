using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Qusasa;

internal static class ThemeIcon
{
    internal static BitmapSource Render()
    {
        var fill = (Brush)Application.Current.FindResource("Accent");
        var ink = (Brush)Application.Current.FindResource("AccentInk");
        var outline = new Pen(ink, 2.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRoundedRectangle(fill, null, new Rect(2, 2, 60, 60), 13, 13);
            drawing.DrawRoundedRectangle(null, outline, new Rect(19, 16, 26, 34), 4, 4);
            drawing.DrawRoundedRectangle(ink, null, new Rect(25, 12, 14, 8), 3, 3);
            foreach (int y in new[] { 27, 34, 41 })
                drawing.DrawLine(outline, new Point(25, y), new Point(y == 41 ? 34 : 39, y));
        }
        var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    internal static System.Drawing.Bitmap CopyBitmap(BitmapSource source)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        stream.Position = 0;
        using var bitmap = new System.Drawing.Bitmap(stream);
        return new System.Drawing.Bitmap(bitmap);
    }

    internal static System.Drawing.Icon CreateTrayIcon(BitmapSource source)
    {
        using var bitmap = CopyBitmap(source);
        using var small = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(32, 32));
        IntPtr handle = small.GetHicon();
        try
        {
            using var icon = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)icon.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
}
