using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Qusasa;

internal static class CrashDiagnostics
{
    private static readonly object gate = new();
    private static readonly string file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qusasa", "Diagnostics", "last-crash.dat");

    internal static void Record(Exception error)
    {
        lock (gate)
        try
        {
            string report = BuildReport(error);
            string folder = Path.GetDirectoryName(file)!;
            Directory.CreateDirectory(folder);
            string temporary = file + ".tmp";
            File.WriteAllBytes(temporary, ProtectedData.Protect(Encoding.UTF8.GetBytes(report.Trim()), null, DataProtectionScope.CurrentUser));
            if (File.Exists(file)) File.Replace(temporary, file, null);
            else File.Move(temporary, file);
            File.SetAttributes(file, FileAttributes.Hidden);
        }
        catch (Exception ex) when (HistoryStore.IsStorageError(ex) || ex is CryptographicException) { }
    }

    internal static string BuildReport(Exception error)
    {
        var frames = new StackTrace(error, false).GetFrames() ?? Array.Empty<StackFrame>();
        var methods = frames.Select(frame => frame.GetMethod())
            .Where(method => method?.DeclaringType?.Assembly == typeof(App).Assembly)
            .Take(4).Select(method => method!.DeclaringType!.FullName + "." + method.Name);
        return "Qusasa " + typeof(App).Assembly.GetName().Version + "\nUTC " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
            "\nWindows " + Environment.OSVersion.Version + "\n" + (Environment.Is64BitProcess ? "x64" : "x86") +
            "\n" + error.GetType().FullName + " (0x" + error.HResult.ToString("X8") + ")\n" + string.Join("\n", methods);
    }

    internal static string? Read()
    {
        lock (gate)
        try
        {
            if (!File.Exists(file) || new FileInfo(file).Length > 16384) return null;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (HistoryStore.IsStorageError(ex) || ex is CryptographicException) { return null; }
    }

    internal static void Dismiss(string report)
    {
        lock (gate)
        try { if (Read() == report) File.Delete(file); }
        catch (Exception ex) when (HistoryStore.IsStorageError(ex)) { }
    }
}
