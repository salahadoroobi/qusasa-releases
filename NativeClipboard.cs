using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Qusasa;

internal sealed class NativeClipboard : IDisposable
{
    private const int ClipboardUpdate = 0x031D;
    private const int Hotkey = 0x0312;
    private const int HotkeyId = 37;
    private const uint UnicodeText = 13;
    private const uint BitmapFormat = 2;
    private readonly uint urlFormat = RegisterClipboardFormat("UniformResourceLocatorW");
    private readonly uint legacyUrlFormat = RegisterClipboardFormat("UniformResourceLocator");
    private readonly uint excludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private readonly uint historyFormat = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private readonly uint pngFormat = RegisterClipboardFormat(ImageCodec.ClipboardFormat);
    private readonly uint[] tableFormats = { RegisterClipboardFormat("Biff8"), RegisterClipboardFormat("Biff12"),
        RegisterClipboardFormat("XML Spreadsheet"), RegisterClipboardFormat("Csv") };
    private readonly uint rtfFormat = RegisterClipboardFormat("Rich Text Format");
    private readonly uint htmlFormat = RegisterClipboardFormat("HTML Format");
    private readonly HwndSource source;
    private readonly DispatcherTimer retry;
    private readonly DispatcherTimer registrationRetry;
    private readonly Action<Snippet> capture;
    private readonly Action largeImage;
    private readonly Action showWindow;
    private uint lastSequence;
    private int attempts;

    public bool ListeningAvailable { get; private set; }
    public bool HotkeyAvailable { get; }
    public bool TranslationHotkeyAvailable { get; }
    public Action? TranslationRequested { get; set; }
    public bool Enabled { get; set; }
    public event Action? ListeningRestored;

    public NativeClipboard(HwndSource source, Action<Snippet> capture, Action largeImage, Action showWindow)
    {
        this.source = source;
        this.capture = capture;
        this.largeImage = largeImage;
        this.showWindow = showWindow;
        retry = new DispatcherTimer(DispatcherPriority.Background, source.Dispatcher) { Interval = TimeSpan.FromMilliseconds(55) };
        retry.Tick += ReadPending;
        lastSequence = GetClipboardSequenceNumber();
        source.AddHook(OnMessage);
        ListeningAvailable = AddClipboardFormatListener(source.Handle);
        registrationRetry = new DispatcherTimer(DispatcherPriority.Background, source.Dispatcher) { Interval = TimeSpan.FromSeconds(2) };
        registrationRetry.Tick += (_, __) =>
        {
            ListeningAvailable = AddClipboardFormatListener(source.Handle);
            if (!ListeningAvailable) return;
            registrationRetry.Stop();
            ListeningRestored?.Invoke();
            if (Enabled && lastSequence != GetClipboardSequenceNumber())
            {
                attempts = 0;
                retry.Start();
            }
        };
        if (!ListeningAvailable) registrationRetry.Start();
        HotkeyAvailable = RegisterHotKey(source.Handle, HotkeyId, 0x4006, 0x20); // Ctrl + Shift + Space
        TranslationHotkeyAvailable = RegisterHotKey(source.Handle, HotkeyId + 3, 0x4003, 0x54); // Ctrl + Alt + T
    }

    public Action? CircleRequested { get; set; }
    private bool circleRegistered;

    public bool SetCircleEnabled(bool enabled)
    {
        if (circleRegistered == enabled) return true;
        if (enabled) circleRegistered = RegisterHotKey(source.Handle, HotkeyId + 1, 0x4003, 0x51);
        else { UnregisterHotKey(source.Handle, HotkeyId + 1); circleRegistered = false; }
        return circleRegistered == enabled;
    }

    public Action? ScannerRequested { get; set; }
    private bool scannerRegistered;

    public bool SetScannerEnabled(bool enabled)
    {
        if (scannerRegistered == enabled) return true;
        if (enabled) scannerRegistered = RegisterHotKey(source.Handle, HotkeyId + 2, 0x4003, 0x4F);
        else { UnregisterHotKey(source.Handle, HotkeyId + 2); scannerRegistered = false; }
        return scannerRegistered == enabled;
    }

    public void SkipCurrent()
    {
        lastSequence = GetClipboardSequenceNumber();
        retry.Stop();
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ClipboardUpdate && Enabled && lastSequence != GetClipboardSequenceNumber())
        {
            attempts = 0;
            retry.Start();
        }
        if (message == Hotkey && wParam.ToInt32() == HotkeyId)
        {
            showWindow();
            handled = true;
        }
        if (message == Hotkey && wParam.ToInt32() == HotkeyId + 1)
        {
            source.Dispatcher.BeginInvoke(new Action(() => CircleRequested?.Invoke()));
            handled = true;
        }
        if (message == Hotkey && wParam.ToInt32() == HotkeyId + 2)
        {
            source.Dispatcher.BeginInvoke(new Action(() => ScannerRequested?.Invoke()));
            handled = true;
        }
        if (message == Hotkey && wParam.ToInt32() == HotkeyId + 3)
        {
            source.Dispatcher.BeginInvoke(new Action(() => TranslationRequested?.Invoke()));
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ReadPending(object? sender, EventArgs args)
    {
        if (!Enabled || lastSequence == GetClipboardSequenceNumber())
        {
            retry.Stop();
            return;
        }
        if (!OpenClipboard(source.Handle))
        {
            if (++attempts >= 8) retry.Stop();
            return;
        }
        string? text = null;
        byte[]? png = null;
        BitmapSource? bitmap = null;
        bool tooLarge = false;
        var files = new List<string>();
        try
        {
            if ((excludeFormat == 0 || !IsClipboardFormatAvailable(excludeFormat)) && !HistoryDisabled())
            {
                ReadFileList(files);
                if (files.Count == 0)
                {
                    text = ReadUnicodeText(UnicodeText);
                    var link = Snippet.WebAddress(text ?? "") ??
                        Snippet.WebAddress(ReadUnicodeText(urlFormat) ?? ReadLegacyUrl() ?? "");
                    if (link != null) text = link;
                    else if ((text == null || !new Snippet { Text = text }.IsFilePath) &&
                        !PreferText(text, tableFormats.Any(HasFormat), HasFormat(rtfFormat) || HasFormat(htmlFormat), HasFormat(pngFormat)))
                    {
                        png = ReadPng(out tooLarge);
                        if (png == null) { bitmap = ReadBitmap(out bool oversized); tooLarge |= oversized; }
                    }
                }
            }
            lastSequence = GetClipboardSequenceNumber();
        }
        finally
        {
            CloseClipboard();
            retry.Stop();
        }
        try
        {
            if (png != null) bitmap = ImageCodec.Decode(png);
            if (bitmap != null)
            {
                var clip = ImageCodec.Encode(bitmap);
                if (clip != null) capture(clip);
                else largeImage();
            }
            else if (files.Count > 0)
            {
                var paths = new List<string>();
                foreach (string path in files)
                {
                    if (!IsImagePath(path)) { paths.Add(path); continue; }
                    try
                    {
                        var clip = ReadImageFile(path);
                        if (clip != null) capture(clip);
                        else { largeImage(); paths.Add(path); }
                    }
                    catch (Exception ex) when (ImageCodec.IsImagingError(ex) || ex is UnauthorizedAccessException)
                    { paths.Add(path); }
                }
                if (paths.Count > 0) capture(new Snippet { Text = string.Join(Environment.NewLine, paths) });
            }
            else if (!string.IsNullOrWhiteSpace(text)) capture(new Snippet { Text = text! });
            else if (tooLarge) largeImage();
        }
        catch (Exception exception) when (ImageCodec.IsImagingError(exception)) { }
    }

    private static bool HasFormat(uint format) => format != 0 && IsClipboardFormatAvailable(format);

    internal static bool PreferText(string? text, bool spreadsheet, bool formattedText, bool explicitImage) =>
        !string.IsNullOrWhiteSpace(text) && (spreadsheet || text!.Contains("\t") || (formattedText && !explicitImage));

    internal static Snippet? ReadImageFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 64L * 1024 * 1024) return null;
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return ImageCodec.Encode(frame);
    }

    private static void ReadFileList(List<string> files)
    {
        const uint fileDrop = 15;
        if (!IsClipboardFormatAvailable(fileDrop)) return;
        ReadDroppedPaths(GetClipboardData(fileDrop), files);
    }

    internal static void ReadDroppedPaths(IntPtr handle, List<string> files)
    {
        if (handle == IntPtr.Zero) return;
        uint count = DragQueryFile(handle, uint.MaxValue, null, 0);
        for (uint i = 0; i < Math.Min(count, (uint)History.MaxEntries); i++)
        {
            int size = (int)DragQueryFile(handle, i, null, 0) + 1;
            var path = new StringBuilder(size);
            DragQueryFile(handle, i, path, (uint)size);
            if (path.Length != 0) files.Add(path.ToString());
        }
    }

    private static bool IsImagePath(string file) => Path.GetExtension(file).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp";

    [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr handle, uint index, StringBuilder? path, uint size);

    private byte[]? ReadPng(out bool tooLarge)
    {
        tooLarge = false;
        if (pngFormat == 0 || !IsClipboardFormatAvailable(pngFormat)) return null;
        IntPtr data = GetClipboardData(pngFormat);
        if (data == IntPtr.Zero) return null;
        ulong length = GlobalSize(data).ToUInt64();
        tooLarge = length > ImageCodec.MaxPngBytes;
        if (length < 33 || tooLarge) return null;
        IntPtr buffer = GlobalLock(data);
        if (buffer == IntPtr.Zero) return null;
        try
        {
            var bytes = new byte[(int)length];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return ImageCodec.TryDimensions(bytes, out _, out _) ? bytes : null;
        }
        finally { GlobalUnlock(data); }
    }

    private static BitmapSource? ReadBitmap(out bool tooLarge)
    {
        tooLarge = false;
        if (!IsClipboardFormatAvailable(BitmapFormat)) return null;
        IntPtr bitmap = GetClipboardData(BitmapFormat);
        if (bitmap == IntPtr.Zero || GetBitmapObject(bitmap, Marshal.SizeOf(typeof(BitmapDescriptor)), out var descriptor) == 0)
            return null;
        tooLarge = !ImageCodec.AllowsDimensions(descriptor.Width, descriptor.Height);
        if (tooLarge) return null;
        try
        {
            // WIC copies the pixels while the system still owns and locks the clipboard bitmap.
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception exception) when (ImageCodec.IsImagingError(exception)) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapDescriptor
    {
        public int Type, Width, Height, RowBytes;
        public ushort Planes, BitsPerPixel;
        public IntPtr Pixels;
    }

    private bool HistoryDisabled()
    {
        if (historyFormat == 0 || !IsClipboardFormatAvailable(historyFormat)) return false;
        IntPtr data = GetClipboardData(historyFormat);
        if (data == IntPtr.Zero || GlobalSize(data).ToUInt64() < sizeof(int)) return false;
        IntPtr buffer = GlobalLock(data);
        if (buffer == IntPtr.Zero) return false;
        try { return Marshal.ReadInt32(buffer) == 0; }
        finally { GlobalUnlock(data); }
    }

    private static string? ReadUnicodeText(uint format)
    {
        if (format == 0 || !IsClipboardFormatAvailable(format)) return null;
        IntPtr handle = GetClipboardData(format);
        if (handle == IntPtr.Zero) return null;
        IntPtr buffer = GlobalLock(handle);
        if (buffer == IntPtr.Zero) return null;
        try
        {
            int length = (int)Math.Min(GlobalSize(handle).ToUInt64() / 2, (ulong)History.MaxTextLength + 1);
            string contents = Marshal.PtrToStringUni(buffer, length) ?? "";
            int end = contents.IndexOf('\0');
            return end >= 0 ? CleanCopiedText(contents.Substring(0, end)) : length <= History.MaxTextLength ? CleanCopiedText(contents) : null;
        }
        finally { GlobalUnlock(handle); }
    }

    private string? ReadLegacyUrl()
    {
        if (legacyUrlFormat == 0 || !IsClipboardFormatAvailable(legacyUrlFormat)) return null;
        IntPtr handle = GetClipboardData(legacyUrlFormat);
        if (handle == IntPtr.Zero) return null;
        int length = (int)Math.Min(GlobalSize(handle).ToUInt64(), 8193UL);
        if (length == 0) return null;
        IntPtr buffer = GlobalLock(handle);
        if (buffer == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringAnsi(buffer, length)?.Split('\0')[0]; }
        finally { GlobalUnlock(handle); }
    }

    // Some web editors expose a serialized space reference in their plain-text copy.
    internal static string CleanCopiedText(string text)
    {
        if (text.IndexOf("&#", StringComparison.Ordinal) < 0) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"&#(?:[xX]0*20|0*32);", " ");
    }

    public void Dispose()
    {
        retry.Stop();
        registrationRetry.Stop();
        if (source.IsDisposed) return;
        if (ListeningAvailable) RemoveClipboardFormatListener(source.Handle);
        SetCircleEnabled(false);
        SetScannerEnabled(false);
        if (TranslationHotkeyAvailable) UnregisterHotKey(source.Handle, HotkeyId + 3);
        if (HotkeyAvailable) UnregisterHotKey(source.Handle, HotkeyId);
        source.RemoveHook(OnMessage);
    }

    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetBitmapObject(IntPtr bitmap, int bufferLength, out BitmapDescriptor descriptor);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string format);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
}
