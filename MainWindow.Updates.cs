using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Qusasa;

public partial class MainWindow
{
    private const string UpdateRegistry = @"Software\Qusasa\Updates";
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private UpdateChannel.Release? availableUpdate;
    private string? downloadedUpdate;
    private bool checkingUpdates, downloadingUpdate, offeredUpdate;
    private int updatePercent;

    private void StartUpdateTimer()
    {
        updateTimer.Tick += (_, __) => _ = CheckUpdates(false);
        updateTimer.Start();
        Dispatcher.BeginInvoke(new Action(() => _ = CheckUpdates(false)), DispatcherPriority.ApplicationIdle);
    }

    private void UpdateModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded || updatingSettings || UpdateModeMenu.SelectedValue is not string mode) return;
        var next = history.Copy();
        next.ManualUpdates = mode == "Manual";
        if (!Commit(next)) { UpdateSettings(); return; }
        if (!next.ManualUpdates) _ = CheckUpdates(false);
    }

    private async void CheckUpdateClick(object sender, RoutedEventArgs e) => await CheckUpdates(true);

    private async Task CheckUpdates(bool manual)
    {
        if (quit) return;
        if (manual)
        {
            SetPanel(UpdateOverlay, UpdateLater);
            RenderUpdate();
        }
        if (checkingUpdates || downloadingUpdate || downloadedUpdate != null) return;
        if (!manual && history.ManualUpdates) return;
        if (!manual && availableUpdate != null) { OfferUpdate(); return; }
        Uri? feed = UpdateChannel.Feed;
        if (feed == null)
        {
            if (manual) UpdateStatus.Text = Ui.T("قناة التحديث غير مهيأة في هذا الإصدار.");
            return;
        }
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            if (manual) UpdateStatus.Text = Ui.T("اتصل بالإنترنت للبحث عن تحديث.");
            return;
        }

        try
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using (var key = Registry.CurrentUser.CreateSubKey(UpdateRegistry))
            {
                if (!manual && Equals(key.GetValue("LastCheck"), today)) return;
                key.SetValue("LastCheck", today);
            }
            checkingUpdates = true;
            RenderUpdate();
            availableUpdate = await UpdateChannel.Check(feed);
            if (quit) return;
            if (availableUpdate == null)
            {
                offeredUpdate = false;
                if (manual && activePanel == UpdateOverlay) UpdateStatus.Text = Ui.T("لديك أحدث إصدار من قصاصة.");
            }
            else if (!manual) OfferUpdate();
        }
        catch (Exception ex) when (UpdateChannel.IsUpdateError(ex))
        {
            if (!quit && manual && activePanel == UpdateOverlay) UpdateStatus.Text = Ui.T("تعذّر التحقق من التحديث.");
        }
        finally
        {
            checkingUpdates = false;
            if (!quit && availableUpdate != null) RenderUpdate();
        }
    }

    private void OfferUpdate()
    {
        if (offeredUpdate || availableUpdate == null) return;
        if (activePrompt != null || activePanel != null || editing || circling) return;
        offeredUpdate = true;
        if (IsVisible && WindowState != WindowState.Minimized) SetPanel(UpdateOverlay, UpdateLater);
        else
        {
            UpdateDownloadNotice.Visibility = Visibility.Visible;
            UpdateNoticeButton.Content = Ui.T("يتوفر تحديث لقصاصة.");
            if (history.DesktopNotifications) tray?.ShowBalloonTip(4500, Ui.T("قصاصة"),
                Ui.T("يتوفر تحديث لقصاصة."), System.Windows.Forms.ToolTipIcon.Info);
        }
        RenderUpdate();
    }

    private void RenderUpdate()
    {
        UpdateAction.Visibility = !checkingUpdates && !downloadingUpdate && availableUpdate != null
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Visibility = downloadingUpdate ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Value = updatePercent;
        UpdateLater.Content = downloadingUpdate ? Ui.T("متابعة في الخلفية") :
            availableUpdate != null && downloadedUpdate == null ? Ui.T("لاحقًا") : Ui.T("رجوع");
        UpdateAction.Content = downloadedUpdate != null ? Ui.T("تثبيت التحديث") : Ui.T("تنزيل التحديث");
        var notes = availableUpdate?.NotesFor(Ui.Language);
        UpdateHighlights.Text = notes ?? "";
        UpdateStatus.Text = checkingUpdates ? Ui.T("جارٍ البحث عن تحديث…") :
            downloadingUpdate ? string.Format(Ui.Culture, Ui.T("تنزيل التحديث: {0}%"), updatePercent) :
            downloadedUpdate != null ? Ui.T("اكتمل التنزيل. التحديث جاهز للتثبيت.") :
            availableUpdate != null ? Ui.T("الإصدار ") + availableUpdate.Version : "";
    }

    private void UpdateLaterClick(object? sender, RoutedEventArgs? e)
    {
        if (!downloadingUpdate && downloadedUpdate == null)
        {
            offeredUpdate = true;
            UpdateDownloadNotice.Visibility = Visibility.Collapsed;
        }
        SetPanel(AboutOverlay, CheckUpdateButton);
    }

    private void UpdateNoticeClick(object sender, RoutedEventArgs e)
    {
        SetPanel(UpdateOverlay, UpdateLater);
        RenderUpdate();
    }

    private async void UpdateActionClick(object sender, RoutedEventArgs e)
    {
        var release = availableUpdate;
        if (release == null || downloadingUpdate) return;
        if (downloadedUpdate is string installer)
        {
            try
            {
                await Task.Run(() => UpdateChannel.VerifyInstaller(installer, release));
                if (!quit) Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) when (UpdateChannel.IsUpdateError(ex))
            {
                downloadedUpdate = null;
                RenderUpdate();
                UpdateStatus.Text = Ui.T("تعذّر تشغيل المثبّت. أعد تنزيل التحديث.");
            }
            return;
        }

        downloadingUpdate = true;
        updatePercent = 0;
        RenderUpdate();
        ShowUpdateDownloadNotice(Ui.T("تنزيل التحديث: {0}%"), 0);
        string? errorMessage = null;
        try
        {
            downloadedUpdate = await UpdateChannel.Download(release, new Progress<int>(percent =>
            {
                if (quit || !downloadingUpdate) return;
                updatePercent = percent;
                RenderUpdate();
                ShowUpdateDownloadNotice(Ui.T("تنزيل التحديث: {0}%"), percent);
            }));
            if (!quit) ShowUpdateDownloadNotice(Ui.T("اكتمل التنزيل. التحديث جاهز للتثبيت."));
        }
        catch (Exception ex) when (UpdateChannel.IsUpdateError(ex))
        {
            errorMessage = Ui.T("تعذّر تنزيل التحديث. حاول مرة أخرى.");
            if (!quit) ShowUpdateDownloadNotice(errorMessage);
        }
        finally
        {
            downloadingUpdate = false;
            if (!quit)
            {
                RenderUpdate();
                if (errorMessage != null) UpdateStatus.Text = errorMessage;
            }
        }
    }

    private void ShowUpdateDownloadNotice(string text, int? percent = null)
    {
        UpdateNoticeButton.Content = percent.HasValue ? string.Format(Ui.Culture, text, percent.Value) : text;
        UpdateDownloadNotice.Visibility = Visibility.Visible;
    }
}
