using System.IO;
using System.Text.Json;
using ModernAssetTool.App.Models;

namespace ModernAssetTool.App.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string SettingsPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                try
                {
                    var preferredDirectory = Path.Combine(appData, "AssetTool");
                    Directory.CreateDirectory(preferredDirectory);
                    return Path.Combine(preferredDirectory, "settings.json");
                }
                catch
                {
                }
            }

            var fallbackDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
            Directory.CreateDirectory(fallbackDirectory);
            return Path.Combine(fallbackDirectory, "settings.json");
        }
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? new AppSettings();
            settings.Webhooks ??= new WebhookSettings();
            if (string.IsNullOrWhiteSpace(settings.Webhooks.Primary))
            {
                settings.Webhooks.Primary = new WebhookSettings().Primary;
            }

            if (string.IsNullOrWhiteSpace(settings.Webhooks.Secondary))
            {
                settings.Webhooks.Secondary = new WebhookSettings().Secondary;
            }

            return settings;
        }
        catch
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
