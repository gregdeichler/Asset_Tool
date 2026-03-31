using System.Windows;
using ModernAssetTool.App.Models;

namespace ModernAssetTool.App;

public partial class RenameCredentialsWindow : Window
{
    public RenameCredentialsWindow(string domainName)
    {
        InitializeComponent();
        DomainText.Text = string.IsNullOrWhiteSpace(domainName) ? "ad.vassar.edu" : domainName;
    }

    public RenameCredentials? Credentials { get; private set; }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordTextBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Enter both a username and password.";
            return;
        }

        Credentials = new RenameCredentials
        {
            Username = username,
            Password = password
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
