using System;
using Microsoft.Win32;

namespace Qusasa;

internal static class WindowsStartup
{
    internal static string? InitialLanguage =>
        Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Qusasa", "InstallerLanguage", null) as string;

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Qusasa";
    internal const string BackgroundArgument = "--background";
    private static string Command => FormatCommand(typeof(App).Assembly.Location);

    internal static bool IsEnabled() => IsEnabledFor(typeof(App).Assembly.Location);

    internal static bool IsEnabledFor(string executable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (!string.Equals(key?.GetValue(ValueName) as string, FormatCommand(executable), StringComparison.OrdinalIgnoreCase))
            return false;
        using var approval = Registry.CurrentUser.OpenSubKey(ApprovedRunKey);
        return approval?.GetValue(ValueName) is not byte[] state || state.Length == 0 || (state[0] & 1) == 0;
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
            // An explicit opt-in clears an old disable decision for this same Run entry.
            using var approval = Registry.CurrentUser.OpenSubKey(ApprovedRunKey, writable: true);
            if (approval?.GetValue(ValueName) is byte[] state && state.Length != 0 && (state[0] & 1) != 0)
                approval.DeleteValue(ValueName, false);
        }
        else if (IsCurrentRegistration(key))
            key.DeleteValue(ValueName, false);
    }

    private static bool IsCurrentRegistration(RegistryKey? key) =>
        string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);

    private static string FormatCommand(string executable) => "\"" + executable + "\" " + BackgroundArgument;
}
