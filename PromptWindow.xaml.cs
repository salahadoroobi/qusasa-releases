using System.Windows;

namespace Qusasa;

public partial class PromptWindow : Window
{
    internal PromptWindow(string title, string message, string? action, string icon)
    {
        InitializeComponent();
        Title = PromptTitle.Text = title;
        PromptMessage.Text = message;
        PromptIcon.Text = icon;
        AcceptButton.Content = action ?? Ui.T("حسنًا");
        if (action == null)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            AcceptButton.IsDefault = true;
            AcceptButton.IsCancel = true;
            AcceptButton.Style = (Style)FindResource("Primary");
        }
        else AcceptButton.Style = (Style)FindResource("DangerButton");
        Loaded += (_, __) => (action == null ? AcceptButton : CancelButton).Focus();
    }

    private void AcceptClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
