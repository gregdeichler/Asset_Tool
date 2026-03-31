namespace ModernAssetTool.App.Models;

public sealed class AppSettings
{
    public WebhookSettings Webhooks { get; set; } = new();
}

public sealed class WebhookSettings
{
    public string Primary { get; set; } = "https://hooks.zapier.com/hooks/catch/4589486/ohn64j0/";
    public string Secondary { get; set; } = "https://hooks.zapier.com/hooks/catch/4589486/omvymjz/";
}

