using System.Windows;

namespace ModernAssetTool.App;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow(string primaryWebhook, string secondaryWebhook)
    {
        InitializeComponent();
        PrimaryWebhookTextBox.Text = primaryWebhook;
        SecondaryWebhookTextBox.Text = secondaryWebhook;
        StatusText.Text = "Ready.";
    }

    public string PrimaryWebhook => PrimaryWebhookTextBox.Text.Trim();
    public string SecondaryWebhook => SecondaryWebhookTextBox.Text.Trim();

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(PrimaryWebhook, UriKind.Absolute, out var primaryUri) ||
            !Uri.TryCreate(SecondaryWebhook, UriKind.Absolute, out var secondaryUri) ||
            (primaryUri.Scheme != Uri.UriSchemeHttp && primaryUri.Scheme != Uri.UriSchemeHttps) ||
            (secondaryUri.Scheme != Uri.UriSchemeHttp && secondaryUri.Scheme != Uri.UriSchemeHttps))
        {
            StatusText.Text = "Both values must be valid http or https URLs.";
            return;
        }

        DialogResult = true;
    }
}
