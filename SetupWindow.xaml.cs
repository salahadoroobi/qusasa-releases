using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Qusasa;

public partial class SetupWindow : Window
{
    private readonly string directory = InstallDirectory();
    private readonly FrameworkElement[] pages;
    private readonly Border[] steps;
    private readonly string[,] strings =
    {
        { "التعريف", "Introduction", "はじめに", "了解应用", "Découverte", "Introducción" },
        { "الخيارات", "Options", "設定", "设置", "Options", "Opciones" },
        { "المراجعة", "Review", "確認", "确认", "Vérification", "Revisión" },
        { "التثبيت", "Installation", "インストール", "安装", "Installation", "Instalación" },
        { "الإكمال", "Finish", "完了", "完成", "Terminer", "Finalizar" },
        { "سجل الحافظة والالتقاط والمسح الضوئي في تطبيق واحد.", "Clipboard history, capture and text recognition in one app.", "履歴、画面キャプチャ、文字認識をひとつのアプリで。", "在一个应用中使用剪贴板历史、截图和文字识别。", "Historique, capture et reconnaissance de texte dans une seule application.", "Historial del portapapeles, capturas y reconocimiento de texto en una sola aplicación." },
        { "اختر كيف تريد الوصول إلى قصاصة.", "Choose how you want to open Qusasa.", "起動方法を選択してください。", "选择打开库萨萨的方式。", "Choisissez comment ouvrir Qusasa.", "Elige cómo quieres abrir Qusasa." },
        { "راجع المسار والخيارات قبل بدء التثبيت.", "Review the destination and options before installing.", "インストール先と設定を確認してください。", "安装前请确认路径和设置。", "Vérifiez le dossier et les options avant l’installation.", "Revisa la carpeta y las opciones antes de instalar." },
        { "تُجهّز الآن ملفات قصاصة ونماذج المسح المحلي.", "Preparing Qusasa and the offline recognition models.", "アプリとオフライン認識モデルを準備しています。", "正在准备库萨萨和离线识别模型。", "Préparation de Qusasa et des modèles de reconnaissance hors ligne.", "Preparando Qusasa y los modelos de reconocimiento sin conexión." },
        { "اكتمل التثبيت. يمكنك فتح قصاصة الآن.", "Installation complete. Qusasa is ready to open.", "インストールが完了しました。アプリを開けます。", "安装完成。现在可以打开库萨萨。", "Installation terminée. Vous pouvez ouvrir Qusasa.", "Instalación completada. Ya puedes abrir Qusasa." },
        { "استعد النصوص والصور والروابط والمسارات. التقط الشاشة واستخرج النص محليًا بست لغات.", "Retrieve text, images, links and paths. Capture your screen and recognize text offline in six languages.", "テキスト、画像、リンク、パスを再利用できます。画面を撮影し、6 言語の文字を端末上で認識できます。", "找回文本、图片、链接和路径。截取屏幕并离线识别六种语言。", "Retrouvez textes, images, liens et chemins. Capturez l’écran et reconnaissez le texte hors ligne en six langues.", "Recupera textos, imágenes, enlaces y rutas. Captura la pantalla y reconoce texto sin conexión en seis idiomas." },
        { "اختصار على سطح المكتب", "Desktop shortcut", "デスクトップにショートカットを作成", "创建桌面快捷方式", "Raccourci sur le bureau", "Acceso directo en el escritorio" },
        { "التشغيل في الخلفية عند تسجيل الدخول", "Start in the background at sign-in", "サインイン時にバックグラウンドで起動", "登录时在后台启动", "Démarrer en arrière-plan à la connexion", "Iniciar en segundo plano al iniciar sesión" },
        { "مسار التثبيت", "Installation folder", "インストール先", "安装位置", "Dossier d’installation", "Carpeta de instalación" },
        { "مفعّل", "On", "オン", "开启", "Activé", "Activado" },
        { "متوقّف", "Off", "オフ", "关闭", "Désactivé", "Desactivado" },
        { "قصاصة قيد التشغيل. اختر «إغلاق وتحديث» للمتابعة بعد حفظ أي تعديلات.", "Qusasa is running. Save any edits, then choose Close and update to continue.", "Qusasa は実行中です。変更を保存してから「閉じて更新」を選択してください。", "库萨萨正在运行。保存修改后，选择“关闭并更新”。", "Qusasa est ouvert. Enregistrez vos modifications, puis choisissez Fermer et mettre à jour.", "Qusasa está abierto. Guarda los cambios y selecciona Cerrar y actualizar." },
        { "إغلاق وتحديث", "Close and update", "閉じて更新", "关闭并更新", "Fermer et mettre à jour", "Cerrar y actualizar" },
        { "تعذّر إكمال التثبيت. أغلق قصاصة من منطقة الإعلام ثم أعد المحاولة.", "Installation could not finish. Quit Qusasa from the notification area and try again.", "インストールに失敗しました。通知領域から終了して、再試行してください。", "安装未能完成。请从通知区域退出后重试。", "Installation impossible. Quittez Qusasa depuis la zone de notification et réessayez.", "No se pudo completar la instalación. Cierra Qusasa desde el área de notificación e inténtalo de nuevo." },
        { "التالي", "Continue", "次へ", "继续", "Continuer", "Continuar" },
        { "تثبيت", "Install", "インストール", "安装", "Installer", "Instalar" },
        { "اضغط Ctrl + Shift + Space لفتح سجل الحافظة من أي تطبيق.", "Press Ctrl + Shift + Space to open the history from any app.", "Ctrl + Shift + Space でどのアプリからでも履歴を開けます。", "在任意应用中按 Ctrl + Shift + Space 打开历史。", "Ctrl + Maj + Espace ouvre l’historique depuis n’importe quelle application.", "Pulsa Ctrl + Mayús + Espacio para abrir el historial desde cualquier aplicación." }
    };

    private int stage;
    private int locale;
    private bool needsClose;
    private bool working;
    internal int ExitCode { get; private set; } = 2;
    private static string InstallDirectory()
    {
        var registered = Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{CF398BDC-5347-4D5D-8F60-AF48B8EEAE38}_is1",
            "InstallLocation", null) as string;
        if (registered != null && !string.IsNullOrWhiteSpace(registered) && Path.IsPathRooted(registered.Trim('"')) &&
            File.Exists(Path.Combine(registered.Trim('"'), "Qusasa.exe"))) return registered.Trim('"').TrimEnd('\\');
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Qusasa");
    }

    private string Target => Path.Combine(directory, "Qusasa.exe");
    private string L(int key) => strings[key, locale];

    public SetupWindow()
    {
        Ui.Apply(null);
        InitializeComponent();
        pages = new FrameworkElement[] { IntroPage, OptionsPage, ReviewPage, InstallPage, DonePage };
        steps = new[] { IntroStep, OptionsStep, ReviewStep, InstallStep, DoneStep };
        SetupLanguage.SelectedValuePath = "Tag";
        SetupLanguage.SelectedValue = Ui.Language;
        Desktop.IsChecked = !File.Exists(Target) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "قصاصة.lnk"));
        Startup.IsChecked = WindowsStartup.IsEnabledFor(Target);
        RenderPage();
        Closing += (_, e) => e.Cancel = working;
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (pages == null || SetupLanguage.SelectedValue is not string name) return;
        Ui.Apply(name);
        locale = SetupLanguage.SelectedIndex;
        RenderPage();
    }

    private void RenderPage()
    {
        var labels = new[] { IntroLabel, OptionsLabel, ReviewLabel, InstallLabel, DoneLabel };
        string[] numbers = { "01", "02", "03", "04", "05" };
        for (int index = 0; index < pages.Length; index++)
        {
            pages[index].Visibility = index == stage ? Visibility.Visible : Visibility.Collapsed;
            steps[index].SetResourceReference(Border.BackgroundProperty, index == stage ? "Tint" : "Panel");
            labels[index].SetResourceReference(TextBlock.ForegroundProperty, index == stage ? "Accent" : "Secondary");
            labels[index].Text = numbers[index] + "   " + L(index);
        }
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Ui.Culture.IetfLanguageTag);
        VersionLabel.Text = Ui.T("الإصدار ").TrimEnd();
        PageVersion.Text = typeof(App).Assembly.GetName().Version.ToString(2);
        PageTitle.Text = stage == 0 ? Ui.T("قصاصة") : L(stage);
        PageDescription.Text = L(5 + stage);
        FeatureText.Text = L(10);
        DesktopLabel.Text = L(11);
        StartupLabel.Text = L(12);
        InstallPathLabel.Text = L(13);
        InstallPathValue.Text = directory;
        ReviewTasks.Text = L(11) + "  ·  " + L(Desktop.IsChecked == true ? 14 : 15) + "\n" +
            L(12) + "  ·  " + L(Startup.IsChecked == true ? 14 : 15);
        DoneText.Text = L(21);
        BackButton.Content = Ui.T(stage == 0 ? "إلغاء" : "رجوع");
        BackButton.Visibility = stage >= 3 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = stage == 4 ? Ui.T("فتح قصاصة") : stage == 2 ? L(needsClose ? 17 : 20) : L(19);
        NextButton.IsEnabled = !working;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        if (!working) Close();
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (stage == 0) Close();
        else { stage--; RenderPage(); }
    }

    private async void NextClick(object sender, RoutedEventArgs e)
    {
        if (stage == 4)
        {
            try { Process.Start(new ProcessStartInfo(Target) { UseShellExecute = true })?.Dispose(); }
            catch (Win32Exception) { DoneText.Text = Ui.T("تعذّر إكمال العملية"); return; }
            Close();
            return;
        }
        if (stage < 2)
        {
            stage++;
            if (stage == 2)
            {
                needsClose = File.Exists(Target) && UpgradeShutdown.Run(Target, false) == 10;
                ReviewStatus.Text = needsClose ? L(16) : "";
            }
            RenderPage();
            return;
        }
        if (working) return;
        working = true;
        stage = 3;
        RenderPage();
        Progress.IsIndeterminate = true;
        ProgressText.Text = L(8);

        string work = Path.Combine(Path.GetTempPath(), "Qusasa-Setup-" + Guid.NewGuid().ToString("N"));
        string progressFile = Path.Combine(work, "progress.txt");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, __) =>
        {
            try
            {
                if (!File.Exists(progressFile) || !int.TryParse(File.ReadAllText(progressFile), out int percent)) return;
                Progress.Value = Math.Max(0, Math.Min(100, percent));
                Progress.IsIndeterminate = false;
                ProgressText.Text = Progress.Value.ToString("0", Ui.Culture) + "%";
            }
            catch (IOException) { }
        };

        try
        {
            string tasks = string.Join(",", new[] { Desktop.IsChecked == true ? "desktopicon" : "", Startup.IsChecked == true ? "startup" : "" }).Trim(',');
            string language = Ui.Language;
            bool consent = needsClose;
            timer.Start();
            bool installed = await Task.Run(() => Install(work, progressFile, tasks, language, consent));
            stage = installed ? 4 : 2;
            if (installed) ExitCode = 0;
            else ReviewStatus.Text = L(18);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException)
        {
            stage = 2;
            ReviewStatus.Text = L(18);
        }
        finally
        {
            timer.Stop();
            working = false;
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (stage == 2) needsClose = File.Exists(Target) && UpgradeShutdown.Run(Target, false) == 10;
            RenderPage();
        }
    }

    private bool Install(string work, string progress, string tasks, string language, bool consent)
    {
        if (File.Exists(Target))
        {
            int status = UpgradeShutdown.Run(Target, false);
            if (status != 0 && (status != 10 || !consent || UpgradeShutdown.Run(Target, true) != 0)) return false;
        }
        Directory.CreateDirectory(work);
        string engine = Path.Combine(work, "payload.exe");
        var payload = Application.GetResourceStream(new Uri("/" + typeof(App).Assembly.GetName().Name + ";component/InstallerPayload.exe", UriKind.Relative));
        using (var resource = payload.Stream)
        using (var file = File.Create(engine)) resource.CopyTo(file);
        string arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=" + language +
            " /DIR=\"" + directory + "\" /TASKS=\"" + tasks + "\" /QPROGRESS=\"" + progress + "\"";
        using var process = Process.Start(new ProcessStartInfo(engine, arguments) { UseShellExecute = false, CreateNoWindow = true });
        if (process == null) return false;
        process.WaitForExit();
        return process.ExitCode == 0 && File.Exists(Target);
    }
}




