namespace ModernAssetTool.App.Models;

public sealed class InventorySnapshot
{
    public string ComputerName { get; set; } = "Unavailable";
    public string Manufacturer { get; set; } = "Unavailable";
    public string Model { get; set; } = "Unavailable";
    public string OSCaption { get; set; } = "Windows";
    public string OSVersion { get; set; } = "Unavailable";
    public string Architecture { get; set; } = "Unavailable";
    public string Serial { get; set; } = "Unavailable";
    public string UUID { get; set; } = "Unavailable";
    public string FormFactor { get; set; } = "Undefined";
    public string Memory { get; set; } = "Unavailable";
    public bool IsDomainJoined { get; set; }
    public string DomainName { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
}
