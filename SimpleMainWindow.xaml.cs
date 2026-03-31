using System.Windows;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ModernAssetTool.App.Models;
using ModernAssetTool.App.Services;

namespace ModernAssetTool.App;

public partial class SimpleMainWindow : Window
{
    private static readonly Regex ComputerNameRegex = new("^[A-Za-z0-9-]{1,15}$", RegexOptions.Compiled);
    private readonly AppSettingsService _settingsService = new();
    private readonly InventoryService _inventoryService = new();
    private readonly WebhookService _webhookService = new();
    private AppSettings _settings = new();
    private InventorySnapshot _inventory = new();

    public SimpleMainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_OnLoaded;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        _inventory = await _inventoryService.GetInventoryAsync();
        ApplyInventoryToUi();
        AdminStateText.Text = _webhookService.IsAdministrator() ? "Elevated session detected" : "Standard session";
        StatusText.Text = "Ready.";
    }

    private void ApplyInventoryToUi()
    {
        ComputerText.Text = _inventory.ComputerName;
        ManufacturerText.Text = _inventory.Manufacturer;
        ModelText.Text = _inventory.Model;
        ManufacturerSummaryText.Text = _inventory.Manufacturer;
        ModelSummaryText.Text = _inventory.Model;
        OSText.Text = $"{_inventory.OSCaption} ({_inventory.OSVersion}) {_inventory.Architecture}";
        SerialText.Text = _inventory.Serial;
        UuidText.Text = _inventory.UUID;
        FormFactorText.Text = _inventory.FormFactor;
        MemoryText.Text = _inventory.Memory;
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing inventory...";
        _inventory = await _inventoryService.GetInventoryAsync();
        ApplyInventoryToUi();
        StatusText.Text = "Inventory refreshed.";
    }

    private async void PreferencesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new PreferencesWindow(_settings.Webhooks.Primary, _settings.Webhooks.Secondary) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settings.Webhooks.Primary = window.PrimaryWebhook;
            _settings.Webhooks.Secondary = window.SecondaryWebhook;
            await _settingsService.SaveAsync(_settings);
            StatusText.Text = "Preferences updated.";
        }
    }

    private async void SubmitButton_OnClick(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var assetTag = AssetTagTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(assetTag))
        {
            StatusText.Text = "Username and asset tag are required.";
            MessageBox.Show(this, "Enter both a username and an asset tag before saving the assignment.", "Missing information", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (AddLocalAdminCheckBox.IsChecked == true && !_webhookService.IsAdministrator())
        {
            StatusText.Text = "The local administrator option requires elevation.";
            MessageBox.Show(this, "Run the app as Administrator to use the local administrator option.", "Elevation required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var renameRequested = RenameComputerCheckBox.IsChecked == true;
        var newComputerName = NewComputerNameTextBox.Text.Trim();
        if (renameRequested)
        {
            var validationError = ValidateComputerName(newComputerName);
            if (validationError is not null)
            {
                StatusText.Text = validationError;
                MessageBox.Show(this, validationError, "Invalid computer name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_webhookService.IsAdministrator())
            {
                StatusText.Text = "Computer renaming requires elevation.";
                MessageBox.Show(this, "Run the app as Administrator to rename the computer.", "Elevation required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var completedSteps = new List<string>();
        var notices = new List<string>();
        RenameCredentials? renameCredentials = null;

        try
        {
            StatusText.Text = "Refreshing device information...";
            _inventory = await _inventoryService.GetInventoryAsync();
            ApplyInventoryToUi();

            if (renameRequested && _inventory.IsDomainJoined)
            {
                var credentialWindow = new RenameCredentialsWindow(_inventory.DomainName) { Owner = this };
                if (credentialWindow.ShowDialog() == true)
                {
                    renameCredentials = credentialWindow.Credentials;
                    notices.Add($"Using provided domain credentials for rename in {_inventory.DomainName}.");
                }
                else
                {
                    renameRequested = false;
                    notices.Add("Computer rename was skipped.");
                }
            }

            StatusText.Text = "Sending primary asset update...";
            await _webhookService.SubmitPrimaryAsync(_settings, _inventory, username, assetTag);
            completedSteps.Add("primary asset webhook sent");

            StatusText.Text = "Sending hardware detail update...";
            await _webhookService.SubmitSecondaryAsync(_settings, _inventory);
            completedSteps.Add("secondary hardware webhook sent");

            if (renameRequested)
            {
                StatusText.Text = "Renaming computer...";
                await _webhookService.RenameComputerAsync(newComputerName, renameCredentials);
                completedSteps.Add($"computer renamed to {newComputerName}");
            }

            if (AddLocalAdminCheckBox.IsChecked == true)
            {
                StatusText.Text = "Adding local administrator access...";
                var adminResult = await _webhookService.AddToLocalAdministratorsAsync(username);
                if (adminResult.AlreadyMember)
                {
                    notices.Add($"{username} is already a local administrator.");
                    completedSteps.Add($"local administrator access already existed for {username}");
                }
                else
                {
                    completedSteps.Add($"local administrator access granted to {username}");
                }
            }

            var message = "The asset information was submitted successfully.";
            if (renameRequested)
            {
                message += " The computer name was updated. A restart is required for the rename to fully apply.";
            }

            if (notices.Count > 0)
            {
                message += $"{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, notices)}";
            }

            StatusText.Text = renameRequested ? "Asset info submitted. Restart required for rename." : "Asset info submitted.";
            MessageBox.Show(this, message, "Asset Tool", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Submit failed: {ex.Message}";
            var details = completedSteps.Count == 0
                ? ex.Message
                : $"Completed before failure: {string.Join(", ", completedSteps)}.{Environment.NewLine}{Environment.NewLine}{ex.Message}";
            MessageBox.Show(this, details, "Submit failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? ValidateComputerName(string computerName)
    {
        if (string.IsNullOrWhiteSpace(computerName))
        {
            return "Enter a new computer name or leave Rename computer unchecked.";
        }

        if (!ComputerNameRegex.IsMatch(computerName))
        {
            return "Computer names must be 1-15 characters and use only letters, numbers, or hyphens.";
        }

        if (computerName.StartsWith('-') || computerName.EndsWith('-'))
        {
            return "Computer names cannot start or end with a hyphen.";
        }

        return null;
    }
}
