using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace Qusasa;

internal static class ShellIcons
{
    internal static void Update(BitmapSource source)
    {
        object? shell = null;
        try
        {
            string executable = typeof(App).Assembly.Location;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qusasa", "Icons");
            Directory.CreateDirectory(folder);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var png = new MemoryStream();
            encoder.Save(png);
            byte[] bytes = png.ToArray();
            string key;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 16);
            string icon = Path.Combine(folder, key + ".ico");
            if (!File.Exists(icon)) WriteIcon(icon, source, bytes);

            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true));
            if (shell == null) return;
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string pinned = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            UpdateShortcut(shell, Path.Combine(programs, "قصاصة", "قصاصة.lnk"), executable, icon);
            UpdateShortcut(shell, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "قصاصة.lnk"), executable, icon);
            if (Directory.Exists(pinned))
                foreach (string shortcut in Directory.EnumerateFiles(pinned, "*.lnk"))
                    UpdateShortcut(shell, shortcut, executable, icon);
        }
        catch (Exception ex) when (IsShellError(ex)) { }
        finally { if (shell != null) Marshal.ReleaseComObject(shell); }
    }

    private static void WriteIcon(string path, BitmapSource source, byte[] png)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)source.PixelWidth);
        writer.Write((byte)source.PixelHeight);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(png.Length);
        writer.Write(22);
        writer.Write(png);
    }

    private static void UpdateShortcut(dynamic shell, string path, string executable, string icon)
    {
        if (!File.Exists(path)) return;
        object? handle = null;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            handle = link;
            string target = link.TargetPath;
            if (!string.Equals(target, executable, StringComparison.OrdinalIgnoreCase)) return;
            string location = icon + ",0";
            if (string.Equals((string)link.IconLocation, location, StringComparison.OrdinalIgnoreCase)) return;
            link.IconLocation = location;
            link.Save();
            SHChangeNotify(0x2000, 0x1005, path, IntPtr.Zero); // SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSHNOWAIT
        }
        catch (Exception ex) when (IsShellError(ex)) { }
        finally { if (handle != null) Marshal.ReleaseComObject(handle); }
    }

    private static bool IsShellError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
        ex is COMException || ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string path, IntPtr other);
}
