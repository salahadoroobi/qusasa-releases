using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Qusasa;

public partial class App : Application
{
    private Mutex? instanceLock;
    private Mutex? installerLock;
    private EventWaitHandle? showSignal;
    private RegisteredWaitHandle? showRegistration;
    private bool ownsLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if SETUP_FRONTEND
        ApplyTheme("Dark");
        var setup = new SetupWindow();
        MainWindow = setup;
        setup.Closed += (_, __) => Shutdown(setup.ExitCode);
        setup.Show();
        return;
#endif
        if (e.Args.Length == 2 && (e.Args[0] == UpgradeShutdown.ProbeArgument || e.Args[0] == UpgradeShutdown.CloseArgument))
        {
            Shutdown(UpgradeShutdown.Run(e.Args[1], e.Args[0] == UpgradeShutdown.CloseArgument));
            return;
        }
        bool background = e.Args.Contains(WindowsStartup.BackgroundArgument, StringComparer.OrdinalIgnoreCase);
        string identity = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        string prefix = @"Local\SalahDin.Qusasa." + identity;
        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Show");
        instanceLock = new Mutex(true, prefix, out ownsLock);
        if (!ownsLock)
        {
            if (!background) showSignal.Set();
            Shutdown();
            return;
        }

        installerLock = new Mutex(false, @"Local\SalahDin.Qusasa.Setup");
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qusasa");
        DispatcherUnhandledException += (_, error) =>
        {
            CrashDiagnostics.Record(error.Exception);
            error.Handled = true;
            MessageBox.Show(Ui.T("تعطّل قصاصة. افتحه مجددًا لمراجعة البلاغ وإرساله."), Ui.T("قصاصة"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
        {
            if (error.ExceptionObject is Exception exception) CrashDiagnostics.Record(exception);
        };
        var window = new MainWindow(new HistoryStore(folder));
        MainWindow = window;
        bool crashOffered = false;
        window.IsVisibleChanged += (_, __) =>
        {
            if (!crashOffered && window.IsVisible && CrashDiagnostics.Read() is string report)
            {
                crashOffered = true;
                // Defer until the initial layout and release-note checks finish.
                Dispatcher.BeginInvoke(new Action(() => window.OfferCrashReport(report)), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        };
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showSignal,
            (_, __) => Dispatcher.BeginInvoke(new Action(window.ShowWindow)), null, Timeout.Infinite, false);
        if (background) new WindowInteropHelper(window).EnsureHandle();
        else window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        showRegistration?.Unregister(null);
        if (ownsLock) instanceLock?.ReleaseMutex();
        instanceLock?.Dispose();
        installerLock?.Dispose();
        showSignal?.Dispose();
        base.OnExit(e);
    }

    private static string? activeTheme;
    private static bool activeGlass;

    internal static void ApplyTheme(string name, bool glass = false)
    {
        glass = glass && !SystemParameters.HighContrast;
        if (activeTheme == name && activeGlass == glass) return;
        string path = "/" + typeof(App).Assembly.GetName().Name + ";component/" + name + ".xaml";
        var theme = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
        if (glass) GlassTheme.Apply(theme);
        Current.Resources.MergedDictionaries[0] = theme;
        if (activeTheme != name) Current.Resources["AppMarkImage"] = ThemeIcon.Render();
        activeTheme = name;
        activeGlass = glass;
    }
}
