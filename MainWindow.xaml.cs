using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Qusasa;

public partial class MainWindow : Window
{
    private readonly HistoryStore store;
    private readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer shellReadyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private History history;
    private NativeClipboard? clipboard;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? trayCapture;
    private System.Drawing.Icon? trayIcon;
    private BitmapSource? trayIconSource;
    private PromptWindow? activePrompt;
    private ImageNotificationWindow? imageNotification;
    private Grid? activePanel;
    private Control? panelFocus;
    private bool aboutFromTray;
    private bool loaded, refreshing, editing, quit, readFailure, writeFailure, updatingSettings;
    private Guid? editingId;
    private Guid? previewedImageId;

    internal MainWindow(HistoryStore store)
    {
        this.store = store;
        history = store.Read(out readFailure);
        ApplyDeviceTheme();
        Ui.Apply(history.UiLanguage ?? WindowsStartup.InitialLanguage);
        InitializeComponent();
        IsVisibleChanged += (_, __) =>
        {
            if (IsVisible) Dispatcher.BeginInvoke(new Action(ShowUpdateNotes));
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += DeviceChanged;
        AboutVersionButton.Content = ReleaseVersion.Text = Ui.T("الإصدار ") + typeof(App).Assembly.GetName().Version.ToString(2);
        ReleaseChanges.ItemsSource = ReleaseNote.Read();
        UpdateFeedback();
        statusTimer.Tick += (_, __) => { statusTimer.Stop(); SetDefaultStatus(); };
        loaded = true;
        ApplyTypography();
        UpdateSettings();
        Refresh();
        SetDefaultStatus();
        StartUpdateTimer();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        clipboard = new NativeClipboard(HwndSource.FromHwnd(handle), Capture,
            () => Notify(Ui.T("تجاوزت الصورة حد الحجم المدعوم."), true), ShowWindow) { Enabled = history.Listening };
        clipboard.CircleRequested = OpenCircle;
        clipboard.ScannerRequested = OpenScreenScanner;
        clipboard.TranslationRequested = () =>
        {
            ShowWindow();
            Notify(Ui.T("الترجمة الفورية") + " · " + Ui.T("قريبًا"));
        };
        TranslationShortcut.Visibility = clipboard.TranslationHotkeyAvailable ? Visibility.Visible : Visibility.Collapsed;
        clipboard.ListeningRestored += () =>
        {
            CaptureButton.IsEnabled = true;
            SetDefaultStatus();
            UpdateSettings();
        };
        if (history.ScannerEnabled && !clipboard.SetScannerEnabled(true))
            Notify(Ui.T("اختصار الماسح مستخدم في تطبيق آخر."), true);
        if (history.CircleEnabled && !clipboard.SetCircleEnabled(true))
            Notify(Ui.T("اختصار التحديد مستخدم في تطبيق آخر."), true);
        HotkeyHint.Visibility = clipboard.HotkeyAvailable ? Visibility.Visible : Visibility.Collapsed;
        if (!clipboard.HotkeyAvailable) HotkeyLabel.Text = Ui.T("اختصار الفتح غير متاح");
        CaptureButton.IsEnabled = clipboard.ListeningAvailable;
        CreateTray();
        UpdateSettings();
    }

    private void CreateTray()
    {
        RefreshTrayIcon();
        var menu = new Forms.ContextMenuStrip
        {
            RightToLeft = !Ui.IsArabic ? Forms.RightToLeft.No : Forms.RightToLeft.Yes,
            Renderer = new TrayMenuRenderer(),
            ImageScalingSize = new System.Drawing.Size(20, 20)
        };
        AddTrayItem(menu, "فتح قصاصة", "\uE77F", () => NavigateFromTray(() => SetPanel(null, editing ? Editor : SearchBox)));
        AddTrayItem(menu, "الإعدادات", "\uE713", () => NavigateFromTray(ShowSettings));
        AddTrayItem(menu, "حول قصاصة", "\uE946", () => NavigateFromTray(() => ShowAbout(fromTray: true)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        trayCapture = AddTrayItem(menu, "إيقاف المراقبة", "\uE769", ToggleCapture);
        menu.Items.Add(new Forms.ToolStripSeparator());
        AddTrayItem(menu, "إنهاء قصاصة", "\uE8BB", () =>
        {
            if (editing && !IsVisible) ShowWindow();
            quit = true;
            Close();
        });
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = Ui.T("قصاصة"), ContextMenuStrip = menu, Visible = true };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowWindow(); };
        tray.BalloonTipClicked += (_, __) => ShowWindow();
        if (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero)
        {
            shellReadyTimer.Tick += (_, __) =>
            {
                if (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero || tray == null) return;
                shellReadyTimer.Stop();
                tray.Visible = false;
                tray.Visible = true;
            };
            shellReadyTimer.Start();
        }
    }

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    private void RefreshTrayIcon()
    {
        var source = (BitmapSource)Application.Current.FindResource("AppMarkImage");
        if (ReferenceEquals(trayIconSource, source)) return;
        var icon = ThemeIcon.CreateTrayIcon(source);
        if (tray != null) tray.Icon = icon;
        trayIcon?.Dispose();
        trayIcon = icon;
        trayIconSource = source;
        ShellIcons.Update(source);
    }

    private static Forms.ToolStripMenuItem AddTrayItem(Forms.ContextMenuStrip menu, string label, string glyph, Action action)
    {
        var item = new Forms.ToolStripMenuItem(Ui.T(label)) { Name = label, Padding = new Forms.Padding(10, 4, 12, 4) };
        TrayMenuRenderer.SetGlyph(item, glyph);
        item.Click += (_, __) => action();
        menu.Items.Add(item);
        return item;
    }

    private void NavigateFromTray(Action navigate)
    {
        ShowWindow();
        if (!quit && activePrompt == null) navigate();
    }

    internal void ShowWindow()
    {
        if (quit) return;
        if (activePrompt != null) { activePrompt.Activate(); return; }
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (panelFocus != null) panelFocus.Focus();
        else if (editing) Editor.Focus();
        else { SearchBox.Focus(); SearchBox.SelectAll(); }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!quit && history.KeepInTray && tray != null)
        {
            e.Cancel = true;
            Hide();
        }
        else if (editing && Editor.Text != (Selected?.Text ?? "") &&
                 !Confirm(Ui.T("تعديلات غير محفوظة"), Ui.T("ستفقد التعديلات الحالية عند إنهاء التطبيق."), Ui.T("إنهاء التطبيق")))
        {
            quit = false;
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= DeviceChanged;
        quit = true;
        DismissImageNotification();
        clipboard?.Dispose();
        statusTimer.Stop();
        shellReadyTimer.Stop();
        updateTimer.Stop();
        if (tray != null)
        {
            tray.Visible = false;
            if (tray.ContextMenuStrip is Forms.ContextMenuStrip menu)
            {
                foreach (var item in menu.Items.OfType<Forms.ToolStripMenuItem>()) item.Image?.Dispose();
                menu.Dispose();
            }
            tray.Dispose();
        }
        trayIcon?.Dispose();
        base.OnClosed(e);
        if (Application.Current is App app) app.Shutdown();
    }

    private Snippet? Selected => ClipList.SelectedItem as Snippet;

    private bool Commit(History next)
    {
        try
        {
            store.Write(next);
            history = next;
            readFailure = writeFailure = false;
            SetDefaultStatus();
            return true;
        }
        catch (Exception exception) when (HistoryStore.IsStorageError(exception))
        {
            writeFailure = true;
            Notify(Ui.T("تعذّر حفظ التغيير على هذا الجهاز."), true);
            return false;
        }
    }

    private void Capture(Snippet clip)
    {
        if (!history.Listening) return;
        if (!AddSnippet(clip, false)) return;
        Refresh();
        NotifyCopy(clip, CopyMessage(clip, saved: true));
    }

    private bool AddSnippet(Snippet candidate, bool manual)
    {
        var next = history.Copy();
        var existing = next.Clips.FirstOrDefault(item => item.ContentKey == candidate.ContentKey);
        if (existing != null && !manual && ReferenceEquals(existing, next.Clips[0])) return false;
        if (existing != null) next.Clips.Remove(existing);
        var clip = existing?.With(moveToNow: true) ?? candidate;
        next.Clips.Insert(0, clip);
        if (!next.Fits())
        {
            Notify(Ui.T("السجل ممتلئ. صدّر القصاصات التي تريد حفظها ثم احذف ما لم تعد تحتاج إليه."), true);
            return false;
        }
        return Commit(next);
    }

    private DateTime? SelectedHistoryDay => (HistoryDayMenu.SelectedItem as ComboBoxItem)?.Tag is DateTime day ? day : null;
    private string? historyDaysLanguage;

    private void RefreshHistoryDays()
    {
        var days = history.Clips.Select(clip => clip.HistoryDay).Distinct().OrderByDescending(day => day).ToArray();
        if (historyDaysLanguage == Ui.Language && HistoryDayMenu.Items.Count > 0 &&
            HistoryDayMenu.Items.Cast<ComboBoxItem>().Skip(1).Select(item => (DateTime)item.Tag).SequenceEqual(days)) return;
        var selected = SelectedHistoryDay;
        refreshing = true;
        try
        {
            historyDaysLanguage = Ui.Language;
            HistoryDayMenu.Items.Clear();
            HistoryDayMenu.Items.Add(new ComboBoxItem { Content = Ui.T("كل الأيام") });
            HistoryDayMenu.SelectedIndex = 0;
            foreach (var day in days)
            {
                string label = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var item = new ComboBoxItem { Content = label, Tag = day };
                HistoryDayMenu.Items.Add(item);
                if (day == selected) HistoryDayMenu.SelectedItem = item;
            }
        }
        finally { refreshing = false; }
    }

    private void Refresh(Guid? prefer = null)
    {
        if (!loaded) return;
        if (refreshing) return;
        RefreshHistoryDays();
        DateTime? day = SelectedHistoryDay;
        Guid? wanted = prefer ?? Selected?.Id;
        string query = SearchBox.Text.Trim();
        bool pinnedOnly = PinnedFilter.IsChecked == true;
        bool linksOnly = LinksFilter.IsChecked == true;
        bool textsOnly = TextFilter.IsChecked == true;
        bool imagesOnly = ImagesFilter.IsChecked == true;
        bool pathsOnly = PathsFilter.IsChecked == true;
        var matches = history.Clips
            .Where(clip => (day == null || clip.HistoryDay == day) && (!textsOnly || (!clip.IsImage && !clip.IsLink && !clip.IsFilePath)) && (!pinnedOnly || clip.Pinned) && (!linksOnly || clip.IsLink) && (!imagesOnly || clip.IsImage) && (!pathsOnly || clip.IsFilePath) &&
                (query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(clip.SearchText, query,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
            .OrderByDescending(clip => clip.Pinned)
            .ToList();

        refreshing = true;
        ClipList.ItemsSource = matches;
        ClipList.SelectedItem = matches.FirstOrDefault(clip => clip.Id == wanted) ?? matches.FirstOrDefault();
        refreshing = false;
        CountLabel.Text = matches.Count.ToString("N0", CultureInfo.InvariantCulture) + Ui.T(" من ") +
            history.Clips.Count.ToString("N0", CultureInfo.InvariantCulture);
        ExportDayButton.IsEnabled = day != null;
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListEmpty.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = query.Length > 0 ? Ui.T("لا توجد نتائج") :
            pathsOnly ? Ui.T("لا توجد مسارات") : textsOnly ? Ui.T("لا توجد نصوص") : pinnedOnly ? Ui.T("لا توجد قصاصات مثبّتة") : linksOnly ? Ui.T("لا توجد روابط") : imagesOnly ? Ui.T("لا توجد صور") : Ui.T("سجلك فارغ");
        EmptyDetail.Text = query.Length > 0 ? Ui.T("جرّب كلمة أخرى أو غيّر عامل التصفية.") :
            pinnedOnly ? Ui.T("ثبّت القصاصات التي تحتاج إليها باستمرار.") :
            pathsOnly ? Ui.T("تظهر هنا مسارات الملفات والمجلدات التي نسختها.") :
            linksOnly ? Ui.T("تظهر هنا عناوين الويب التي نسختها.") :
            imagesOnly ? Ui.T("انسخ صورة أو التقط لقطة شاشة ليظهر محتواها هنا.") :
            history.Listening && clipboard?.ListeningAvailable != false ? Ui.T("انسخ نصًا أو صورة من أي تطبيق ليظهر هنا.") :
            Ui.T("شغّل المراقبة أو أضف قصاصة يدويًا.");
        ExportButton.IsEnabled = history.Clips.Count > 0;
        ClearButton.IsEnabled = history.Clips.Any(clip => !clip.Pinned);
        if (!editing) UpdateEditor();
    }

    private void UpdateEditor()
    {
        var clip = Selected;
        bool isImage = clip?.IsImage == true;
        Editor.Text = clip?.Text ?? "";
        Editor.ScrollToHome();
        Editor.FlowDirection = clip?.Direction ?? FlowDirection.RightToLeft;
        Editor.Visibility = clip == null || isImage ? Visibility.Collapsed : Visibility.Visible;
        PictureFrame.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
        Guid? imageId = isImage ? clip?.Id : null;
        if (previewedImageId != imageId)
        {
            Picture.Source = clip?.ImagePng is string png ? ImageCodec.Decode(Convert.FromBase64String(png)) : null;
            previewedImageId = imageId;
        }
        PictureError.Visibility = isImage && Picture.Source == null ? Visibility.Visible : Visibility.Collapsed;
        Editor.IsReadOnly = true;
        Editor.IsUndoEnabled = false;
        PreviewTitle.Text = isImage ? Ui.T("معاينة الصورة") : Ui.T("معاينة القصاصة");
        PreviewMeta.Text = clip == null ? Ui.T("النص أو الصورة الكاملة") : clip.Kind + "  ·  " + clip.DateLabel;
        PreviewEmpty.Visibility = clip == null ? Visibility.Visible : Visibility.Collapsed;
        ClipActions.Visibility = clip == null ? Visibility.Collapsed : Visibility.Visible;
        ScanButton.Visibility = isImage && history.ScannerEnabled ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
        PinIcon.Text = clip?.Pinned == true ? "\uE77A" : "\uE718";
        string pinLabel = clip?.Pinned == true ? Ui.T("إلغاء تثبيت القصاصة") : Ui.T("تثبيت القصاصة");
        PinButton.ToolTip = pinLabel;
        AutomationProperties.SetName(PinButton, pinLabel);
        CopyButton.IsEnabled = clip != null && (!isImage || Picture.Source != null);
        UpdateCharacterCount();
    }

    private void SearchChanged(object sender, TextChangedEventArgs e) => Refresh();
    private void FilterChanged(object sender, RoutedEventArgs e) => Refresh();
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loaded && !refreshing && !editing) UpdateEditor();
    }

    private void EditorChanged(object sender, TextChangedEventArgs e)
    {
        if (loaded) UpdateCharacterCount();
    }

    private void UpdateCharacterCount()
    {
        bool isImage = !editing && Selected?.IsImage == true;
        CharacterCount.FlowDirection = isImage ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        CharacterCount.Text = isImage ? Selected!.Dimensions : editing || Selected != null
            ? Editor.Text.Length.ToString("N0", CultureInfo.InvariantCulture) + Ui.T(" حرف") : "";
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(Editor.Text) && Editor.Text.Length <= History.MaxTextLength;
    }

    private void BeginEdit(Snippet? clip)
    {
        if (editing || clip?.IsImage == true) return;
        editing = true;
        editingId = clip?.Id;
        SetEditingControls(true);
        PreviewTitle.Text = clip == null ? Ui.T("قصاصة جديدة") : Ui.T("تحرير القصاصة");
        PreviewMeta.Text = Ui.T("أدخل النص الذي تريد حفظه");
        Editor.FlowDirection = clip?.Direction ?? FlowDirection.RightToLeft;
        Editor.IsUndoEnabled = false;
        Editor.Text = clip?.Text ?? "";
        Editor.IsUndoEnabled = true;
        UpdateCharacterCount();
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    private void SetEditingControls(bool active)
    {
        ClipList.IsEnabled = SearchBox.IsEnabled = HistoryDayMenu.IsEnabled = Filters.IsEnabled = SettingsButton.IsEnabled = NewButton.IsEnabled = !active;
        Editor.IsReadOnly = !active;
        Editor.IsUndoEnabled = active;
        SaveButton.Visibility = CancelEditButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        if (active)
        {
            ClipActions.Visibility = PreviewEmpty.Visibility = PictureFrame.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
        }
    }

    private void EndEdit(Guid? prefer = null)
    {
        editing = false;
        editingId = null;
        SetEditingControls(false);
        Refresh(prefer);
        if (Selected != null) ClipList.ScrollIntoView(Selected);
        NewButton.Focus();
    }

    private void Save()
    {
        if (!editing || string.IsNullOrWhiteSpace(Editor.Text) || Editor.Text.Length > History.MaxTextLength) return;
        string text = Editor.Text;
        if (editingId is Guid id)
        {
            var next = history.Copy();
            int index = next.Clips.FindIndex(clip => clip.Id == id);
            if (index < 0)
            {
                if (!AddSnippet(new Snippet { Text = text }, true)) return;
                id = history.Clips[0].Id;
            }
            else
            {
                var clip = next.Clips[index];
                var duplicate = next.Clips.FirstOrDefault(other => other.Id != id && !other.IsImage && other.Text == text);
                next.Clips[index] = clip.With(text, clip.Pinned || duplicate?.Pinned == true);
                if (duplicate != null) next.Clips.Remove(duplicate);
                if (!next.Fits())
                {
                    Notify(Ui.T("السجل ممتلئ. صدّر القصاصات التي تريد حفظها ثم احذف ما لم تعد تحتاج إليه."), true);
                    return;
                }
                if (!Commit(next)) return;
            }
            EndEdit(id);
        }
        else
        {
            if (!AddSnippet(new Snippet { Text = text }, true)) return;
            Guid newId = history.Clips[0].Id;
            HistoryDayMenu.SelectedIndex = 0;
            SearchBox.Clear();
            AllFilter.IsChecked = true;
            EndEdit(newId);
        }
        Notify(Ui.T("حُفظت القصاصة."));
    }

    private void NewClick(object sender, RoutedEventArgs e) => BeginEdit(null);
    private void EditClick(object sender, RoutedEventArgs e) { if (Selected != null) BeginEdit(Selected); }
    private void SaveClick(object sender, RoutedEventArgs e) => Save();
    private void CancelEditClick(object sender, RoutedEventArgs e) => CancelEdit();
    private void CancelEdit()
    {
        string original = history.Clips.FirstOrDefault(clip => clip.Id == editingId)?.Text ?? "";
        if (Editor.Text == original || Confirm(Ui.T("إلغاء التحرير"), Ui.T("ستفقد التعديلات التي لم تحفظها."), Ui.T("إهمال التعديلات"))) EndEdit(editingId);
    }

    private void CopyClick(object sender, RoutedEventArgs e) => Copy();
    private void ListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject item &&
            ItemsControl.ContainerFromElement(ClipList, item) is ListBoxItem) Copy();
    }
    private void Copy()
    {
        if (editing || Selected is not Snippet clip || !CopyButton.IsEnabled) return;
        try
        {
            if (clip.ImagePng is string png && Picture.Source is BitmapSource image)
            {
                using var dataStream = new MemoryStream(Convert.FromBase64String(png), writable: false);
                var data = new DataObject();
                data.SetData(ImageCodec.ClipboardFormat, dataStream, false);
                data.SetImage(image);
                Clipboard.SetDataObject(data, true);
            }
            else Forms.Clipboard.SetDataObject(clip.Text, true, 5, 30);
            clipboard?.SkipCurrent();
            Notify(Ui.T("نُسخت القصاصة إلى الحافظة."));
            NotifyCopy(clip, CopyMessage(clip, saved: false));
        }
        catch (ExternalException)
        {
            Notify(Ui.T("الحافظة مشغولة. حاول النسخ مرة أخرى."), true);
        }
    }

    internal static string CopyMessage(Snippet clip, bool saved)
    {
        if (clip.IsImage) return Ui.T(saved ? "حُفظت صورة في سجل الحافظة." : "نُسخت الصورة إلى الحافظة.");
        if (clip.IsLink) return Ui.T(saved ? "حُفظ رابط في سجل الحافظة." : "نُسخ الرابط إلى الحافظة.");
        if (clip.IsFilePath) return Ui.T(saved ? "حُفظ المسار في سجل الحافظة." : "نُسخ المسار إلى الحافظة.");
        return Ui.T(saved ? "حُفظت قصاصة نصية في سجل الحافظة." : "نُسخت القصاصة النصية إلى الحافظة.");
    }

    private void NotifyCopy(Snippet clip, string message, bool inHistory = true)
    {
        if (history.CopySound) SystemSounds.Asterisk.Play();
        DismissImageNotification();
        if (!history.DesktopNotifications || !ImageNotificationWindow.CanShow) return;

        var thumbnail = history.NotificationContent && clip.ImagePng is string png ? ImageCodec.DecodeThumbnail(png) : null;
        string? preview = history.NotificationContent && !clip.IsImage ? clip.Caption : null;
        var notification = new ImageNotificationWindow(thumbnail, preview, message,
            inHistory ? () => ShowClip(clip.Id) : ShowWindow);
        imageNotification = notification;
        notification.Closed += (_, __) =>
        {
            if (ReferenceEquals(imageNotification, notification)) imageNotification = null;
        };
        notification.Show();
    }

    private void DismissImageNotification()
    {
        var notification = imageNotification;
        imageNotification = null;
        if (notification?.IsVisible == true) notification.Close();
    }

    private void ShowClip(Guid id)
    {
        ShowWindow();
        if (editing || quit || activePrompt != null || !history.Clips.Any(clip => clip.Id == id)) return;
        SetPanel(null, ClipList);
        HistoryDayMenu.SelectedIndex = 0;
        SearchBox.Clear();
        AllFilter.IsChecked = true;
        Refresh(id);
        if (Selected != null) ClipList.ScrollIntoView(Selected);
    }

    private void PinClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not Snippet clip) return;
        var next = history.Copy();
        next.Clips[next.Clips.FindIndex(item => item.Id == clip.Id)] = clip.With(pinned: !clip.Pinned);
        if (!Commit(next)) return;
        Refresh(clip.Id);
        Notify(clip.Pinned ? Ui.T("أُلغي تثبيت القصاصة.") : Ui.T("ثُبّتت القصاصة."));
    }

    private void DeleteClick(object sender, RoutedEventArgs e) => DeleteSelected();
    private void DeleteSelected()
    {
        if (Selected is not Snippet clip ||
            !Confirm(Ui.T("حذف القصاصة؟"), Ui.T("ستُحذف هذه القصاصة من سجل الحافظة."), Ui.T("حذف القصاصة"), "\uE74D")) return;
        var next = history.Copy();
        next.Clips.RemoveAll(item => item.Id == clip.Id);
        if (!Commit(next)) return;
        Refresh();
        Notify(Ui.T("حُذفت القصاصة."));
    }

    private void ToggleCaptureClick(object sender, RoutedEventArgs e) => ToggleCapture();
    private void ToggleCapture()
    {
        if (clipboard?.ListeningAvailable == false) return;
        var next = history.Copy();
        next.Listening = !history.Listening;
        if (!Commit(next)) return;
        if (clipboard != null)
        {
            clipboard.SkipCurrent();
            clipboard.Enabled = history.Listening;
        }
        UpdateSettings();
        Refresh();
    }

    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();

    private void ShowSettings()
    {
        UpdateSettings();
        SetPanel(SettingsOverlay, CloseSettingsButton);
    }

    private void OpenAbout(object sender, RoutedEventArgs e) => ShowAbout(fromTray: false);

    private void ShowAbout(bool fromTray)
    {
        aboutFromTray = fromTray;
        SetPanel(AboutOverlay, AboutBackButton);
    }

    private void CloseAbout(object? sender, RoutedEventArgs? e)
    {
        if (aboutFromTray) CloseSettings(null, null);
        else SetPanel(SettingsOverlay, AboutButton);
    }

    private void OpenWhatsNew(object sender, RoutedEventArgs e) => SetPanel(WhatsNewOverlay, WhatsNewBackButton);
    private bool firstOpenNotes;
    private bool checkedUpdateNotes;

    private void ShowUpdateNotes()
    {
        if (!IsVisible || activePanel != null || checkedUpdateNotes) return;
        checkedUpdateNotes = true;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Qusasa", writable: true);
            string version = typeof(App).Assembly.GetName().Version.ToString(2);
            if (!Equals(key?.GetValue("PendingReleaseNotes"), version)) return;
            firstOpenNotes = true;
            WhatsNewBackButton.SetResourceReference(ContentControl.ContentProperty, "UiClose");
            SetPanel(WhatsNewOverlay, WhatsNewBackButton);
            key!.DeleteValue("PendingReleaseNotes", throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) { }
    }

    private void CloseWhatsNew(object? sender, RoutedEventArgs? e)
    {
        if (!firstOpenNotes) { SetPanel(AboutOverlay, AboutVersionButton); return; }
        firstOpenNotes = false;
        WhatsNewBackButton.SetResourceReference(ContentControl.ContentProperty, "UiBack");
        CloseSettings(null, null);
    }
    private string? diagnosticReport;
    private bool resumeReleaseNotes;
    private void OpenFeedback(object sender, RoutedEventArgs e)
    {
        ConfigureFeedback(false);
        SetPanel(FeedbackOverlay, FeedbackText);
    }

    internal void OfferCrashReport(string report)
    {
        diagnosticReport = report;
        ShowWindow();
        resumeReleaseNotes = activePanel == WhatsNewOverlay;
        ConfigureFeedback(true);
        SetPanel(FeedbackOverlay, FeedbackText);
    }

    private void ConfigureFeedback(bool reporting)
    {
        if (!reporting) diagnosticReport = null;
        FeedbackCategories.Visibility = reporting ? Visibility.Collapsed : Visibility.Visible;
        IncludeDiagnostics.Visibility = DiagnosticsPreview.Visibility = reporting ? Visibility.Visible : Visibility.Collapsed;
        IncludeDiagnostics.IsChecked = false;
        DiagnosticsPreview.IsExpanded = false;
        DiagnosticText.Text = diagnosticReport ?? "";
        FeedbackDeliveryHint.Text = "";
        FeedbackText.Clear();
        FeedbackTitle.SetResourceReference(TextBlock.TextProperty, reporting ? "UiCrashReport" : "UiShareYourIdea");
        FeedbackPrompt.SetResourceReference(TextBlock.TextProperty, reporting ? "UiDescribeCrash" : "UiWhatWouldYouLikeToAddOr");
        UpdateFeedback();
    }

    private void CloseFeedback(object? sender, RoutedEventArgs? e)
    {
        bool reporting = diagnosticReport != null;
        if (reporting) CrashDiagnostics.Dismiss(diagnosticReport!);
        ConfigureFeedback(false);
        if (reporting && resumeReleaseNotes) SetPanel(WhatsNewOverlay, WhatsNewBackButton);
        else SetPanel(reporting ? null : AboutOverlay, reporting ? SettingsButton : FeedbackButton);
        resumeReleaseNotes = false;
    }

    private void FeedbackChanged(object sender, TextChangedEventArgs e)
    {
        if (loaded) UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        FeedbackCount.Text = FeedbackText.Text.Length.ToString(CultureInfo.InvariantCulture) + " / 260";
        FeedbackChannels.IsEnabled = !string.IsNullOrWhiteSpace(FeedbackText.Text);
    }

    private void OpenFeedbackDraft(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string channel } || string.IsNullOrWhiteSpace(FeedbackText.Text)) return;
        var selected = FeatureChoice.IsChecked == true ? FeatureChoice :
            ImprovementChoice.IsChecked == true ? ImprovementChoice : SuggestionChoice;
        string subject = Ui.T("قصاصة · ") + (diagnosticReport == null ? selected.Content : Ui.T("بلاغ عن عطل"));
        string body = FeedbackText.Text.Trim();
        if (IncludeDiagnostics.IsChecked == true && diagnosticReport != null)
            body += "\r\n\r\n" + diagnosticReport;
        if (channel == "github")
        {
            try
            {
                Forms.Clipboard.SetDataObject(subject + "\r\n\r\n" + body, true, 5, 30);
                clipboard?.SkipCurrent();
                FeedbackDeliveryHint.Text = Ui.T("نُسخ البلاغ. الصقه في رسالة إلى المطوّر على GitHub.");
            }
            catch (ExternalException) { ShowError(Ui.T("تعذّر النسخ إلى الحافظة.")); return; }
        }
        string address = channel switch
        {
            "email" => EmailButton.Tag + "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(body),
            "telegram" => TelegramButton.Tag + "?text=" + Uri.EscapeDataString(subject + "\n\n" + body),
            "whatsapp" => WhatsAppButton.Tag + "?text=" + Uri.EscapeDataString(subject + "\n\n" + body),
            _ => (string)GitHubButton.Tag
        };
        OpenExternalAddress(address, Ui.T("تعذّر فتح الرابط. تحقّق من تطبيقات ويندوز الافتراضية."));
    }

    private void ContactClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string address }) return;
        OpenExternalAddress(address, Ui.T("تعذّر فتح الرابط. تحقّق من تطبيقات ويندوز الافتراضية."));
    }

    private void OpenExternalAddress(string address, string failure)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            ShowError(failure);
        }
    }

    private void CloseSettings(object? sender, RoutedEventArgs? e)
    {
        SetPanel(null, editing ? Editor : SettingsButton);
    }

    private void SetPanel(Grid? panel, Control focus)
    {
        if (activePanel != null && activePanel != panel)
        {
            activePanel.Visibility = Visibility.Collapsed;
            if (scannerWindow != null) { scanRequest++; scannerWindow.Close(); }
        }
        activePanel = panel;
        panelFocus = panel == null ? null : focus;
        Workspace.IsEnabled = panel == null;
        if (panel != null) panel.Visibility = Visibility.Visible;
        focus.Focus();
        if (panel == null) ShowUpdateNotes();
    }

    private void UpdateSettings()
    {
        bool available = clipboard?.ListeningAvailable != false;
        CaptureLabel.Text = !available ? Ui.T("المراقبة غير متاحة") : history.Listening ? Ui.T("المراقبة تعمل") : Ui.T("المراقبة متوقّفة");
        CaptureDot.SetResourceReference(ShapeFill, available && history.Listening ? "Signal" : "Muted");
        if (trayCapture != null)
        {
            trayCapture.Text = history.Listening ? Ui.T("إيقاف المراقبة") : Ui.T("تشغيل المراقبة");
            trayCapture.Enabled = available;
            string glyph = history.Listening ? "\uE769" : "\uE768";
            if (!Equals(trayCapture.Tag, glyph)) TrayMenuRenderer.SetGlyph(trayCapture, glyph);
        }
        updatingSettings = true;
        ThemeMenu.SelectedValue = history.Theme;
        GlassSetting.IsChecked = history.GlassEnabled;
        if (history.ImportedFamily != null)
        {
            var custom = FontMenu.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, "Custom"));
            if (custom == null) { custom = new ComboBoxItem { Tag = "Custom" }; FontMenu.Items.Insert(3, custom); }
            custom.Content = history.ImportedFamily;
        }
        FontMenu.SelectedValue = history.ClipFont;
        SizeMenu.SelectedValue = history.ClipFontSize.ToString(CultureInfo.InvariantCulture);
        string? titleFamily = history.ImportedTitleFamily ?? history.ImportedFamily;
        ImportedTitleChoice.Visibility = titleFamily == null ? Visibility.Collapsed : Visibility.Visible;
        ImportedTitleChoice.Content = titleFamily;
        TitleFontMenu.SelectedValue = history.TitleFont ?? History.DefaultFont;
        TitleMenu.SelectedValue = history.TitleStep.ToString(CultureInfo.InvariantCulture);
        LanguageMenu.SelectedValue = history.UiLanguage ?? WindowsStartup.InitialLanguage ?? "Auto";
        CircleSetting.IsChecked = history.CircleEnabled;
        ScannerSetting.IsChecked = history.ScannerEnabled;
        ScannerModeMenu.SelectedValue = CloudTools.Mode;
        ScanButton.Visibility = Selected?.IsImage == true && history.ScannerEnabled ? Visibility.Visible : Visibility.Collapsed;
        DesktopSetting.IsChecked = history.DesktopNotifications;
        ContentSetting.IsChecked = history.NotificationContent;
        ContentSetting.IsEnabled = history.DesktopNotifications;
        SoundSetting.IsChecked = history.CopySound;
        TraySetting.IsChecked = history.KeepInTray;
        HistoryUnitsMenu.SelectedValue = history.ShortHistoryUnits ? "Short" : "Full";
        UpdateModeMenu.SelectedValue = history.ManualUpdates ? "Manual" : "Auto";
        try
        {
            StartupSetting.IsChecked = WindowsStartup.IsEnabled();
            StartupSetting.IsEnabled = true;
        }
        catch (Exception exception) when (exception is SecurityException || exception is UnauthorizedAccessException || exception is IOException)
        {
            StartupSetting.IsChecked = false;
            StartupSetting.IsEnabled = false;
        }
        updatingSettings = false;
    }

    private static readonly DependencyProperty ShapeFill = System.Windows.Shapes.Shape.FillProperty;

    private void DeviceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => { if (!quit && history.Theme == "Auto") ApplyDeviceTheme(); }));
    }

    private void ApplyDeviceTheme()
    {
        bool light = history.Theme == "Light" || (history.Theme == "Auto" &&
            !Equals(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1), 0));
        App.ApplyTheme(history.Theme is "Mint" or "Midnight" or "Iris" or "Sand" or "Ocean" or "Rose" or "Forest" ? history.Theme : light ? "Light" : "Dark", history.GlassEnabled);
        if (tray != null) RefreshTrayIcon();
        if (tray?.ContextMenuStrip is Forms.ContextMenuStrip menu)
            foreach (var item in menu.Items.OfType<Forms.ToolStripMenuItem>())
                if (item.Tag is string glyph) TrayMenuRenderer.SetGlyph(item, glyph);
    }

    private void GlassChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings) return;
        var next = history.Copy();
        next.GlassEnabled = GlassSetting.IsChecked == true;
        if (Commit(next)) ApplyDeviceTheme();
        UpdateSettings();
    }

    private void AppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded || updatingSettings || sender is not ComboBox menu || menu.SelectedValue is not string option) return;
        var next = history.Copy();
        if (menu == ThemeMenu) next.ThemeMode = option;
        else if (menu == SizeMenu) next.ClipFontSize = int.Parse(option, CultureInfo.InvariantCulture);
        else if (menu == TitleFontMenu)
        {
            if (option == "Import")
            {
                if (!ImportFont(next, headings: true)) { UpdateSettings(); return; }
            }
            else next.TitleFont = option;
        }
        else if (menu == TitleMenu) next.TitleStep = int.Parse(option, CultureInfo.InvariantCulture);
        else if (menu == LanguageMenu) next.UiLanguage = option;
        else if (menu == FontMenu && option != "Import") next.ClipFont = option;
        else if (menu == FontMenu && !ImportFont(next)) { UpdateSettings(); return; }
        if (Commit(next))
        {
            ApplyDeviceTheme();
            if (menu == LanguageMenu)
            {
                Ui.Apply(history.UiLanguage);
                DismissImageNotification();
                StatusLabel.SetResourceReference(TextBlock.TextProperty, "UiHistoryIsEncryptedAndStoredLocally");
                AboutVersionButton.Content = ReleaseVersion.Text = Ui.T("الإصدار ") + typeof(App).Assembly.GetName().Version.ToString(2);
                ReleaseChanges.ItemsSource = ReleaseNote.Read();
                if (clipboard?.HotkeyAvailable == false) HotkeyLabel.Text = Ui.T("اختصار الفتح غير متاح");
                if (tray?.ContextMenuStrip is Forms.ContextMenuStrip trayMenu)
                {
                    trayMenu.RightToLeft = !Ui.IsArabic ? Forms.RightToLeft.No : Forms.RightToLeft.Yes;
                    foreach (var item in trayMenu.Items.OfType<Forms.ToolStripMenuItem>())
                        if (item.Name.Length > 0) item.Text = Ui.T(item.Name);
                    tray.Text = Ui.T("قصاصة");
                }
                Refresh();
                SetDefaultStatus();
            }
            ApplyTypography();
        }
        UpdateSettings();
    }

    private static readonly string FontDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qusasa", "Fonts");

    private bool ImportFont(History next, bool headings = false)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Font (*.ttf;*.otf)|*.ttf;*.otf", Title = Ui.T("استيراد خط") };
        if (dialog.ShowDialog(this) != true) return false;
        try
        {
            StoreImportedFont(next, dialog.FileName, FontDirectory, headings);
            return true;
        }
        catch (Exception ex) when (ImageCodec.IsImagingError(ex) || ex is UnauthorizedAccessException)
        {
            ShowError(Ui.T("تعذّر استيراد الخط."));
            return false;
        }
    }

    internal static void StoreImportedFont(History next, string source, string folder, bool headings)
    {
        var typeface = new GlyphTypeface(new Uri(source));
        var family = typeface.FamilyNames.Values.First();
        Directory.CreateDirectory(folder);
        string file = Guid.NewGuid().ToString("N") + Path.GetExtension(source);
        File.Copy(source, Path.Combine(folder, file));
        if (headings)
        {
            next.ImportedTitleFamily = family;
            next.ImportedTitleFont = file;
            next.TitleFont = "Custom";
        }
        else
        {
            // Preserve an older heading selection that shared the clip font.
            if (next.TitleFont == "Custom" && next.ImportedTitleFont == null)
            {
                next.ImportedTitleFont = next.ImportedFont;
                next.ImportedTitleFamily = next.ImportedFamily;
            }
            next.ImportedFamily = family;
            next.ImportedFont = file;
            next.ClipFont = "Custom";
        }
    }

    private void ApplyTypography()
    {
        foreach (int heading in new[] { 17, 19, 20, 24, 25, 26, 32 })
            Application.Current.Resources["Heading" + heading] = (double)(heading + history.TitleStep);
        Editor.FontFamily = ClipList.FontFamily = ResolveFont(history.ClipFont, history.ImportedFont, history.ImportedFamily);
        Application.Current.Resources["HeadingFont"] = ResolveFont(history.TitleFont,
            history.ImportedTitleFont ?? history.ImportedFont, history.ImportedTitleFamily ?? history.ImportedFamily);
        int size = history.ClipFontSize is 13 or 18 ? history.ClipFontSize : History.DefaultFontSize;
        Editor.FontSize = ClipList.FontSize = size;
    }

    private static FontFamily ResolveFont(string? choice, string? file, string? family)
    {
        if (choice == "Custom" && file != null && family != null && File.Exists(Path.Combine(FontDirectory, file)))
            return new FontFamily(new Uri(FontDirectory + Path.DirectorySeparatorChar), "./" + file + "#" + family);
        return new FontFamily(choice is "Tahoma" or "Arial" ? choice : History.DefaultFont);
    }

    private int scanRequest;
    private bool scanRunning;

    private void ScannerChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings || clipboard == null) return;
        bool enabled = ScannerSetting.IsChecked == true;
        if (clipboard.SetScannerEnabled(enabled))
        {
            var next = history.Copy();
            next.ScannerEnabled = enabled;
            if (!Commit(next)) clipboard.SetScannerEnabled(history.ScannerEnabled);
        }
        else ShowError(Ui.T("اختصار الماسح مستخدم في تطبيق آخر."));
        UpdateSettings();
    }

    private ScannerWindow? scannerWindow;

    private void ScanImage(object sender, RoutedEventArgs e)
    {
        if (history.ScannerEnabled && !scanRunning && !editing && Selected?.ImagePng is string png)
            BeginScan(png);
    }

    private void OpenScreenScanner()
    {
        if (!history.ScannerEnabled || scannerWindow != null || scanRunning || editing || activePrompt != null || quit) return;
        string png;
        circling = true;
        try
        {
            using var selection = new CircleSelection(scanOnly: true);
            if (selection.ShowDialog() != Forms.DialogResult.OK || selection.RegionImage == null) return;
            using var stream = new MemoryStream();
            selection.RegionImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            png = Convert.ToBase64String(stream.ToArray());
        }
        catch (Exception ex) when (ImageCodec.IsImagingError(ex) || ex is Win32Exception)
        {
            ShowWindow();
            ShowError(Ui.T("تعذّر مسح المنطقة المحددة."));
            return;
        }
        finally { circling = false; }
        ShowWindow();
        BeginScan(png);
    }


    private async void BeginScan(string png)
    {
        scanRunning = true;
        ScanButton.IsEnabled = false;
        int request = ++scanRequest;
        
        // Create or show scanner window
        if (scannerWindow == null)
        {
            scannerWindow = new ScannerWindow();
            scannerWindow.ClosedByUser += OnScannerWindowClosed;
            scannerWindow.CopyRequested += OnScannerCopy;
            scannerWindow.SaveRequested += OnScannerSave;
        }
        
        string status = CloudTools.Mode == "Local" ? Ui.T("جارٍ استخراج النص على جهازك…") : CloudLabel("جارٍ استخراج النص…", "Extracting text…");
        scannerWindow.StartScan(status);
        
        string message;
        string text = "";
        bool? fromCloud = null;
        try
        {
            var result = await CloudTools.Scan(png);
            fromCloud = result.Cloud;
            text = NativeClipboard.CleanCopiedText(result.Text).Trim();
            if (text.Length > History.MaxTextLength) text = text.Substring(0, History.MaxTextLength);
            message = text.Length == 0 ? Ui.T("لم يعثر الماسح على نص واضح.") : Ui.T("راجع النص المستخرج وعدّله قبل النسخ أو الحفظ.");
        }
        catch (Exception ex) when (LocalScanner.IsReadError(ex) || CloudTools.IsServiceError(ex))
        {
            message = Ui.T("تعذّر استخراج النص من هذه الصورة.");
        }
        finally
        {
            scanRunning = false;
            ScanButton.IsEnabled = true;
        }
        if (request != scanRequest || scannerWindow == null || quit) return;
        scannerWindow.CompleteScan(text, fromCloud, message);
    }

    private void OnScannerWindowClosed()
    {
        scannerWindow = null;
    }

    private void OnScannerCopy(string text)
    {
        try
        {
            Forms.Clipboard.SetDataObject(text, true, 5, 30);
            clipboard?.SkipCurrent();
            var clip = new Snippet { Text = text };
            NotifyCopy(clip, CopyMessage(clip, saved: false), inHistory: false);
        }
        catch (ExternalException) { }
    }

    private void OnScannerSave(string text)
    {
        if (!AddSnippet(new Snippet { Text = text }, true)) return;
        Refresh();
    }

    private void CircleChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings || clipboard == null) return;
        bool enabled = CircleSetting.IsChecked == true;
        if (clipboard.SetCircleEnabled(enabled))
        {
            var next = history.Copy();
            next.CircleEnabled = enabled;
            if (!Commit(next)) clipboard.SetCircleEnabled(history.CircleEnabled);
        }
        else ShowError(Ui.T("اختصار التحديد مستخدم في تطبيق آخر."));
        UpdateSettings();
    }

    private bool circling;
    private void OpenCircle()
    {
        if (circling || activePrompt != null || quit) return;
        circling = true;
        try
        {
            using var selection = new CircleSelection();
            if (selection.ShowDialog() != Forms.DialogResult.OK || selection.RegionImage == null) return;
            using var stream = new MemoryStream();
            selection.RegionImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = stream.ToArray();
            var bitmap = ImageCodec.Decode(bytes);
            if (bitmap == null)
            {
                Notify(Ui.T("تجاوزت الصورة حد الحجم المدعوم."), true);
                return;
            }
            var clip = new Snippet
            {
                ImagePng = Convert.ToBase64String(bytes),
                ImageWidth = bitmap.PixelWidth,
                ImageHeight = bitmap.PixelHeight
            };
            if (!AddSnippet(clip, true)) return;
            Refresh(clip.Id);
            using var png = new MemoryStream(bytes, writable: false);
            var data = new DataObject();
            data.SetData(ImageCodec.ClipboardFormat, png, false);
            data.SetImage(bitmap);
            Clipboard.SetDataObject(data, true);
            clipboard?.SkipCurrent();
            Notify(Ui.T("نُسخت القصاصة إلى الحافظة."));
            NotifyCopy(clip, Ui.T("نُسخت الصورة إلى الحافظة."));
        }
        catch (Exception ex) when (ImageCodec.IsImagingError(ex) || ex is Win32Exception)
        { Notify(Ui.T("تعذّر نسخ المنطقة المحددة."), true); }
        finally { circling = false; }
    }

    private void NotificationChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings) return;
        var next = history.Copy();
        next.DesktopNotifications = DesktopSetting.IsChecked == true;
        next.NotificationContent = ContentSetting.IsChecked == true;
        next.CopySound = SoundSetting.IsChecked == true;
        Commit(next);
        DismissImageNotification();
        UpdateSettings();
    }

    private void TraySettingChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings) return;
        var next = history.Copy();
        next.KeepInTray = TraySetting.IsChecked == true;
        Commit(next);
        UpdateSettings();
    }

    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded || updatingSettings) return;
        try
        {
            WindowsStartup.SetEnabled(StartupSetting.IsChecked == true);
            UpdateSettings();
        }
        catch (Exception exception) when (exception is SecurityException || exception is UnauthorizedAccessException || exception is IOException)
        {
            UpdateSettings();
            ShowError(Ui.T("تعذّر تغيير إعداد التشغيل مع بدء ويندوز."));
        }
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        string count = history.Clips.Count(clip => !clip.Pinned).ToString("N0", Ui.Culture);
        string summary = string.Format(Ui.Culture, Ui.T("القصاصات التي ستُمسح: {0}"), count);
        if (!Confirm(Ui.T("مسح القصاصات غير المثبّتة؟"), summary + "\n" + SettingsHistorySize.Text + "\n" +
            Ui.T("ستبقى القصاصات المثبّتة في السجل."), Ui.T("مسح السجل"), "\uE74D")) return;
        var next = history.Copy();
        next.Clips.RemoveAll(clip => !clip.Pinned);
        if (!Commit(next)) return;
        UpdateSettings();
        Refresh();
        CloseSettings(null, null);
        Notify(Ui.T("مُسحت القصاصات غير المثبّتة."));
    }

    private void ExportClick(object sender, RoutedEventArgs e) => ExportHistory(history.Clips, DateTime.Today);

    private void ExportDayClick(object sender, RoutedEventArgs e)
    {
        if (SelectedHistoryDay is DateTime day)
            ExportHistory(history.Clips.Where(clip => clip.HistoryDay == day).ToList(), day);
    }

    private void ExportHistory(List<Snippet> clips, DateTime day)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Ui.T("تصدير سجل قصاصة"), FileName = "Qusasa-" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Filter = Ui.T("سجل قصاصة المشفّر (*.qusasa)|*.qusasa"), DefaultExt = ".qusasa", AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            HistoryStore.Export(dialog.FileName.EndsWith(".qusasa", StringComparison.OrdinalIgnoreCase) ?
                dialog.FileName : dialog.FileName + ".qusasa", clips);
            CloseSettings(null, null);
            Notify(Ui.T("صُدّر سجل القصاصات."));
        }
        catch (Exception exception) when (HistoryStore.IsStorageError(exception))
        {
            ShowError(Ui.T("تعذّر تصدير السجل إلى هذا الملف."));
        }
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Ui.T("استيراد قصاصات"), Filter = Ui.T("الملفات المدعومة (*.qusasa;*.json;*.txt)|*.qusasa;*.json;*.txt|سجل قصاصة المشفّر (*.qusasa)|*.qusasa|أرشيف قديم (*.json)|*.json|ملف نصي (*.txt)|*.txt")
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                string text = HistoryStore.ReadText(dialog.FileName);
                if (string.IsNullOrWhiteSpace(text) || text.Length > History.MaxTextLength)
                {
                    ShowError(Ui.T("يجب أن يحتوي ملف TXT على نص لا يتجاوز 500,000 حرف."));
                    return;
                }
                if (!AddSnippet(new Snippet { Text = text }, true)) { ShowError(StatusLabel.Text); return; }
            }
            else if (!Merge(HistoryStore.Import(dialog.FileName))) return;
            HistoryDayMenu.SelectedIndex = 0;
            SearchBox.Clear();
            AllFilter.IsChecked = true;
            Refresh(history.Clips.FirstOrDefault()?.Id);
            CloseSettings(null, null);
            Notify(Ui.T("استُوردت القصاصات."));
        }
        catch (Exception exception) when (HistoryStore.IsStorageError(exception))
        {
            ShowError(Ui.T("تعذّر قراءة الملف. اختر أرشيف قصاصة صالحًا أو ملف TXT بترميز Unicode."));
        }
    }

    private bool Merge(List<Snippet> incoming)
    {
        var next = history.Copy();
        var knownContent = next.Clips.ToDictionary(clip => clip.ContentKey);
        var existingIds = new HashSet<Guid>(next.Clips.Select(clip => clip.Id));
        foreach (var clip in incoming)
        {
            if (knownContent.TryGetValue(clip.ContentKey, out var existing))
            {
                if (!clip.Pinned || existing.Pinned) continue;
                var updated = existing.With(pinned: true);
                next.Clips[next.Clips.IndexOf(existing)] = updated;
                knownContent[clip.ContentKey] = updated;
                continue;
            }
            if (!existingIds.Add(clip.Id))
            {
                do { clip.Id = Guid.NewGuid(); } while (!existingIds.Add(clip.Id));
            }
            next.Clips.Add(clip);
            knownContent.Add(clip.ContentKey, clip);
        }
        if (!next.Fits())
        {
            ShowError(Ui.T("لا توجد مساحة كافية لدمج الملف. احذف بعض القصاصات ثم أعد الاستيراد."));
            return false;
        }
        next.Clips = next.Clips.OrderByDescending(clip => clip.SavedUtc).ToList();
        if (Commit(next)) return true;
        ShowError(Ui.T("تعذّر حفظ السجل المستورد."));
        return false;
    }

    private bool Confirm(string title, string text, string action, string icon = "\uE7BA") =>
        ShowPrompt(title, text, action, icon);

    private void ShowError(string text) => ShowPrompt(Ui.T("تعذّر إكمال العملية"), text, null, "\uE7BA");

    private bool ShowPrompt(string title, string text, string? action, string icon)
    {
        var prompt = new PromptWindow(title, text, action, icon) { Owner = this };
        activePrompt = prompt;
        PromptScrim.Visibility = Visibility.Visible;
        try { return prompt.ShowDialog() == true; }
        finally
        {
            activePrompt = null;
            PromptScrim.Visibility = Visibility.Collapsed;
        }
    }

    private void Notify(string message, bool error = false)
    {
        statusTimer.Stop();
        StatusLabel.Text = message;
        StatusLabel.SetResourceReference(TextBlock.ForegroundProperty, error ? "Danger" : "Secondary");
        StatusGlyph.Text = error ? "\uE7BA" : "\uE73E";
        StatusGlyph.SetResourceReference(TextBlock.ForegroundProperty, error ? "Danger" : "Signal");
        statusTimer.Start();
    }

    private void SetDefaultStatus()
    {
        var bytes = store.StoredBytes();
        string size = bytes is long length ? FormatHistorySize(length, history.ShortHistoryUnits) : "—";
        StatusHistorySize.Text = size;
        SettingsHistorySize.Text = string.Format(Ui.Culture, Ui.T("حجم السجل: {0}"), size);
        StatusLabel.Text = readFailure ? Ui.T("تعذّرت قراءة السجل السابق. أُوقفت المراقبة.") :
            writeFailure ? Ui.T("تعذّر حفظ السجل. جرّب مرة أخرى.") : Ui.T("السجل محفوظ محليًا ومشفّر");
        StatusLabel.SetResourceReference(TextBlock.ForegroundProperty, readFailure || writeFailure ? "Danger" : "Secondary");
        StatusGlyph.SetResourceReference(TextBlock.ForegroundProperty, readFailure || writeFailure ? "Danger" : "Muted");
        StatusGlyph.Text = readFailure || writeFailure ? "\uE7BA" : "\uE72E";
    }

    private void HistoryUnitsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded || updatingSettings || HistoryUnitsMenu.SelectedValue is not string style) return;
        var next = history.Copy();
        next.ShortHistoryUnits = style == "Short";
        if (!Commit(next)) UpdateSettings();
    }

    internal static string FormatHistorySize(long bytes, bool abbreviated = false)
    {
        if (bytes < 1024) return bytes.ToString("N0", Ui.Culture) + " " + (abbreviated ? "B" : Ui.T("بايت"));
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#", Ui.Culture) + " " + (abbreviated ? "KB" : Ui.T("كيلو بايت"));
        return (bytes / (1024.0 * 1024.0)).ToString("0.#", Ui.Culture) + " " + (abbreviated ? "MB" : Ui.T("ميجا بايت"));
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (activePanel != null)
        {
            if (e.Key != Key.Escape) return;
            if (activePanel == FeedbackOverlay) CloseFeedback(null, null);
            else if (activePanel == WhatsNewOverlay) CloseWhatsNew(null, null);
            else if (scannerWindow != null) { scannerWindow.Close(); scannerWindow = null; }
            else if (activePanel == UpdateOverlay) UpdateLaterClick(null, null);
            else if (activePanel == AboutOverlay) CloseAbout(null, null);
            else CloseSettings(null, null);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.F when !editing: SearchBox.Focus(); SearchBox.SelectAll(); break;
                case Key.N when !editing: BeginEdit(null); break;
                case Key.Enter when editing: Save(); break;
                case Key.Enter: Copy(); break;
                default: return;
            }
        }
        else if (e.Key == Key.Escape && editing) CancelEdit();
        else if (e.Key == Key.Delete && ClipList.IsKeyboardFocusWithin && !editing) DeleteSelected();
        else if (e.Key == Key.Enter && ClipList.IsKeyboardFocusWithin && !editing) Copy();
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin && Selected != null)
        {
            ClipList.ScrollIntoView(Selected);
            ClipList.UpdateLayout();
            (ClipList.ItemContainerGenerator.ContainerFromItem(Selected) as ListBoxItem)?.Focus();
        }
        else return;
        e.Handled = true;
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState =
        WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void WindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeGlyph != null) MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }
}

