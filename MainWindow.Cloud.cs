using System;
using System.Windows.Controls;

namespace Qusasa;

public partial class MainWindow
{
    private static string CloudLabel(string ar, string en) => Ui.IsArabic ? ar : en;

    private void ScannerModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded || updatingSettings || ScannerModeMenu.SelectedValue is not string mode) return;
        try { CloudTools.SaveScanMode(mode); }
        catch (Exception ex) when (HistoryStore.IsStorageError(ex))
        {
            ShowError(CloudLabel("تعذّر حفظ وضع المسح.", "Could not save the scan mode."));
            UpdateSettings();
        }
    }
}
