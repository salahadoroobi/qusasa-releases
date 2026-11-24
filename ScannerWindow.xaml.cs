using System;
using System.Windows;
using System.Windows.Input;

namespace Qusasa;

public partial class ScannerWindow : Window
{
    public event Action<string>? CopyRequested;
    public event Action<string>? SaveRequested;
    public event Action? ClosedByUser;

    public ScannerWindow()
    {
        InitializeComponent();
        ShowInTaskbar = false;
        ScanText.Focus();
    }

    public void StartScan(string status)
    {
        ScanText.Clear();
        ScanSourceBadge.Visibility = Visibility.Collapsed;
        ScanText.IsReadOnly = true;
        ScanProgress.Visibility = Visibility.Visible;
        ScanStatus.Text = status;
        Show();
        Activate();
    }

    public void CompleteScan(string text, bool? fromCloud, string message)
    {
        ScanProgress.Visibility = Visibility.Collapsed;
        ScanStatus.Text = message;

        if (fromCloud.HasValue)
        {
            ScanSourceLabel.Text = fromCloud.Value ? Ui.T(\"سحابي\") : Ui.T(\"محلي\");
            ScanSourceBadge.Visibility = Visibility.Visible;
        }

        ScanText.Text = text;
        ScanText.IsReadOnly = false;
        ScanTextChanged(null, null);
        if (text.Length != 0) ScanText.Focus();
    }

    public string ScannedText => ScanText.Text.Trim();

    private void ScanTextChanged(object? sender, TextChangedEventArgs? e)
    {
        bool valid = !string.IsNullOrWhiteSpace(ScanText.Text) && ScanText.Text.Length <= History.MaxTextLength;
        CopyScanButton.IsEnabled = valid;
        SaveScanButton.IsEnabled = valid;
    }

    private void CopyScan(object sender, RoutedEventArgs e)
    {
        if (!CopyScanButton.IsEnabled) return;
        CopyRequested?.Invoke(ScanText.Text.Trim());
        ScanStatus.Text = Ui.T(\"نُسخت القصاصة إلى الحافظة.\");
    }

    private void SaveScan(object sender, RoutedEventArgs e)
    {
        if (!SaveScanButton.IsEnabled) return;
        SaveRequested?.Invoke(ScanText.Text.Trim());
        Close();
    }

    private void WindowClosed(object? sender, EventArgs e)
    {
        ClosedByUser?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }
}
