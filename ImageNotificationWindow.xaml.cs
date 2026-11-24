using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using System.Windows.Threading;

namespace Qusasa;

public partial class ImageNotificationWindow : Window
{
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly Action openClip;

    internal static bool CanShow => SHQueryUserNotificationState(out int state) == 0 && state == 5;

    internal ImageNotificationWindow(ImageSource? image, string? preview, string message, Action openClip)
    {
        this.openClip = openClip;
        InitializeComponent();
        Thumbnail.Source = image;
        ThumbnailFrame.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
        PreviewText.Text = preview ?? "";
        PreviewText.Visibility = string.IsNullOrWhiteSpace(preview) ? Visibility.Collapsed : Visibility.Visible;
        if (preview != null) PreviewText.FlowDirection = new Snippet { Text = preview }.Direction;
        Message.Text = message;
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 14;
        Top = area.Bottom - Height - 14;
        Loaded += (_, __) => PositionNearClock();
        expiry.Tick += (_, __) => Close();
        expiry.Start();
    }

    private void PositionNearClock()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var bar)) return;
        IntPtr tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        IntPtr clock = tray == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(tray, IntPtr.Zero, "TrayClockWClass", null);
        // Recent taskbars may render the clock without a child HWND. The tray still marks that side.
        bool found = TryBounds(clock, out var anchor) || TryBounds(tray, out anchor);
        bool mirrored = (GetWindowLong(taskbar, -20) & 0x00400000) != 0;
        var screen = Forms.Screen.FromHandle(taskbar);
        var handle = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(handle, out var frame)) return;
        int gap = Math.Max(10, (int)Math.Round(14 * VisualTreeHelper.GetDpi(this).DpiScaleX));
        var origin = ClockPosition(screen.WorkingArea, screen.Bounds, bar.Rectangle,
            found ? anchor.Rectangle : (Drawing.Rectangle?)null, frame.Rectangle.Size, gap, mirrored);
        SetWindowPos(handle, IntPtr.Zero, origin.X, origin.Y, 0, 0, 0x0015);
    }

    internal static Drawing.Point ClockPosition(Drawing.Rectangle work, Drawing.Rectangle monitor, Drawing.Rectangle bar,
        Drawing.Rectangle? tray, Drawing.Size notification, int gap, bool mirrored)
    {
        bool horizontal = bar.Width >= bar.Height;
        bool left = tray is Drawing.Rectangle area ? area.Left + area.Width / 2 < monitor.Left + monitor.Width / 2 : mirrored;
        bool top = tray is Drawing.Rectangle section && section.Top + section.Height / 2 < monitor.Top + monitor.Height / 2;
        if (horizontal) top = bar.Top + bar.Height / 2 < monitor.Top + monitor.Height / 2;
        else left = bar.Left + bar.Width / 2 < monitor.Left + monitor.Width / 2;
        return new Drawing.Point(
            left ? work.Left + gap : Math.Max(work.Left, work.Right - notification.Width - gap),
            top ? work.Top + gap : Math.Max(work.Top, work.Bottom - notification.Height - gap));
    }

    private static bool TryBounds(IntPtr window, out NativeRect rect)
    {
        rect = default;
        return window != IntPtr.Zero && GetWindowRect(window, out rect) && rect.Right > rect.Left && rect.Bottom > rect.Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
        internal Drawing.Rectangle Rectangle => Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? name);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    protected override void OnClosed(EventArgs e)
    {
        expiry.Stop();
        Thumbnail.Source = null;
        base.OnClosed(e);
    }

    private void Dismiss(object sender, RoutedEventArgs e) => Close();

    private void OpenClip(object sender, RoutedEventArgs e)
    {
        Close();
        openClip();
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
