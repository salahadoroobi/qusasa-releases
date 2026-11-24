using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Qusasa;

internal static class UpgradeShutdown
{
    internal const string ProbeArgument = "--probe-update";
    internal const string CloseArgument = "--close-for-update";
    private const int Running = 10;
    private const int ClosureFailed = 11;
    private const uint QueryEndSession = 0x0011;
    private const uint EndSession = 0x0016;
    private const uint AbortIfHung = 0x0002;
    private static readonly IntPtr ReplacingFiles = new(1);

    internal static int Run(string executable, bool close)
    {
        string path = Path.GetFullPath(executable);
        if (!string.Equals(Path.GetFileName(path), "Qusasa.exe", StringComparison.OrdinalIgnoreCase)) return ClosureFailed;
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName("Qusasa"))
        {
            using (process)
            {
                if (process.Id == current.Id) continue;
                try
                {
                    if (process.SessionId != current.SessionId ||
                        !string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!close) return Running;
                    if (!Close(process)) return ClosureFailed;
                }
                catch (InvalidOperationException) { } // The instance may have exited during enumeration.
                catch (Win32Exception) { return ClosureFailed; }
            }
        }
        return 0;
    }

    private static bool Close(Process process)
    {
        var windows = new List<IntPtr>();
        uint processId = (uint)process.Id;
        EnumWindows((handle, _) =>
        {
            if (GetWindowThreadProcessId(handle, out uint owner) != 0 && owner == processId)
            {
                var className = new StringBuilder(256);
                GetClassName(handle, className, className.Capacity);
                if (className.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal)) windows.Add(handle);
            }
            return true;
        }, IntPtr.Zero);

        // Include WPF's hidden application window, where older releases receive session messages.
        foreach (IntPtr window in windows)
        {
            if (process.HasExited) return true;
            if (GetWindowThreadProcessId(window, out uint owner) == 0 || owner != processId) continue;
            if (SendMessageTimeout(window, QueryEndSession, IntPtr.Zero, ReplacingFiles, AbortIfHung, 800, out var consent) == IntPtr.Zero)
                continue;
            if (consent == UIntPtr.Zero) return false;
            if (GetWindowThreadProcessId(window, out owner) != 0 && owner == processId)
                SendMessageTimeout(window, EndSession, new IntPtr(1), ReplacingFiles, AbortIfHung, 800, out _);
        }
        return process.WaitForExit(4000);
    }

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder value, int capacity);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
