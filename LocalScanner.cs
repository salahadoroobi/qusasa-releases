using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Qusasa;

internal static class LocalScanner
{
    private const string Models = "fra+eng+spa+ara+jpn+chi_sim";

    private const string Library = "tesseract50.dll";
    private static readonly Lazy<IntPtr> Native = new(LoadEngine);

    internal static string Read(string png)
    {
        if (Native.Value == IntPtr.Zero) throw new DllNotFoundException(Library);
        using var stream = new MemoryStream(Convert.FromBase64String(png), writable: false);
        using var original = new Bitmap(stream);
        double scale = Math.Min(2, Math.Min(4096.0 / Math.Max(original.Width, original.Height),
            Math.Sqrt(8_000_000.0 / ((long)original.Width * original.Height))));
        int width = Math.Max(1, (int)(original.Width * scale));
        int height = Math.Max(1, (int)(original.Height * scale));
        const int margin = 16;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, new Rectangle(0, 0, width, height));
        }
        var pixels = PreparePixels(bitmap, margin);
        bool singleLine = original.Width >= 4L * original.Height && original.Height <= 230;
        var engine = TessBaseAPICreate();
        if (engine == IntPtr.Zero) throw new InvalidOperationException();
        try
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string models = Ui.Language == "es" ? "spa+eng+fra+ara+jpn+chi_sim" : Models;
            if (TessBaseAPIInit3(engine, Encoding.UTF8.GetBytes(folder + "\0"), models) != 0)
                throw new InvalidOperationException();
            var result = Recognize(singleLine ? 7 : 3);
            if (result.Confidence < 65 || result.Text.Length == 0)
            {
                var alternative = Recognize(singleLine ? 13 : 11);
                if (alternative.Text.Length != 0 && (result.Text.Length == 0 || alternative.Confidence > result.Confidence))
                    result = alternative;
            }
            return result.Confidence < 30 ? "" : result.Text;
        }
        finally { TessBaseAPIDelete(engine); }

        (string Text, int Confidence) Recognize(int mode)
        {
            TessBaseAPISetPageSegMode(engine, mode);
            TessBaseAPISetImage(engine, pixels, width + 2 * margin, height + 2 * margin, 1, width + 2 * margin);
            TessBaseAPISetSourceResolution(engine, 300);
            var nativeText = TessBaseAPIGetUTF8Text(engine);
            if (nativeText == IntPtr.Zero) return ("", 0);
            try
            {
                int length = 0;
                while (length < 4 * History.MaxTextLength && Marshal.ReadByte(nativeText, length) != 0) length++;
                var utf8 = new byte[length];
                Marshal.Copy(nativeText, utf8, 0, length);
                string text = Encoding.UTF8.GetString(utf8).Replace("\f", "").Trim();
                if (text.Length > History.MaxTextLength) text = text.Substring(0, History.MaxTextLength);
                return (text, TessBaseAPIMeanTextConf(engine));
            }
            finally { TessDeleteText(nativeText); }
        }
    }

    private static byte[] PreparePixels(Bitmap bitmap, int margin)
    {
        byte[] gray = GrayPixels(bitmap);
        int width = bitmap.Width;
        int height = bitmap.Height;
        long edge = 0;
        for (int x = 0; x < width; x++) edge += gray[x] + gray[(height - 1) * width + x];
        for (int y = 0; y < height; y++) edge += gray[y * width] + gray[y * width + width - 1];
        bool invert = edge < 128L * (2 * width + 2 * height);
        int stride = width + 2 * margin;
        var bordered = new byte[stride * (height + 2 * margin)];
        for (int i = 0; i < bordered.Length; i++) bordered[i] = 255;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte pixel = gray[y * width + x];
                bordered[(y + margin) * stride + x + margin] = invert ? (byte)(255 - pixel) : pixel;
            }
        return bordered;
    }

    private static byte[] GrayPixels(Bitmap bitmap)
    {
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var gray = new byte[bounds.Width * bounds.Height];
        var row = new byte[bounds.Width * 3];
        try
        {
            for (int y = 0; y < bounds.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                for (int x = 0; x < bounds.Width; x++)
                    gray[y * bounds.Width + x] = (byte)((29 * row[3 * x] + 150 * row[3 * x + 1] + 77 * row[3 * x + 2]) >> 8);
            }
            return gray;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static IntPtr LoadEngine()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr", Environment.Is64BitProcess ? "x64" : "x86");
        return LoadLibraryEx(Path.Combine(folder, Library), IntPtr.Zero, 0x1100);
    }

    internal static bool IsReadError(Exception ex) => ImageCodec.IsImagingError(ex) ||
        ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr TessBaseAPICreate();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int TessBaseAPIInit3(IntPtr engine, byte[] dataPath, string language);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void TessBaseAPISetImage(IntPtr engine, byte[] image, int width, int height, int bytesPerPixel, int stride);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void TessBaseAPISetSourceResolution(IntPtr engine, int ppi);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void TessBaseAPISetPageSegMode(IntPtr engine, int mode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int TessBaseAPIMeanTextConf(IntPtr engine);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr TessBaseAPIGetUTF8Text(IntPtr engine);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void TessDeleteText(IntPtr text);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void TessBaseAPIDelete(IntPtr engine);
}


