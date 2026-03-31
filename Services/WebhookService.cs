using System.Diagnostics;
using System.Net.Http;
using System.Text;
using ModernAssetTool.App.Models;

namespace ModernAssetTool.App.Services;

public sealed record LocalAdminResult(bool Added, bool AlreadyMember, string Message);

public sealed class WebhookService
{
    public async Task SubmitPrimaryAsync(AppSettings settings, InventorySnapshot inventory, string username, string assetTag)
    {
        using var httpClient = new HttpClient();

        var primaryResponse = await httpClient.PostAsync(
            settings.Webhooks.Primary,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["querystring__name"] = username,
                ["querystring__computer"] = inventory.ComputerName,
                ["querystring__OS"] = $"{inventory.OSCaption} ({inventory.OSVersion}) {inventory.Architecture}",
                ["querystring__Model"] = inventory.Model,
                ["querystring__Serial"] = inventory.Serial,
                ["querystring__Tag"] = assetTag,
                ["querystring_date"] = inventory.Date
            }));
        primaryResponse.EnsureSuccessStatusCode();
    }

    public async Task SubmitSecondaryAsync(AppSettings settings, InventorySnapshot inventory)
    {
        using var httpClient = new HttpClient();

        var secondaryResponse = await httpClient.PostAsync(
            settings.Webhooks.Secondary,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["querystring__date"] = inventory.Date,
                ["querystring__time"] = inventory.Time,
                ["querystring__formfactor"] = inventory.FormFactor,
                ["querystring__UUID"] = inventory.UUID,
                ["querystring__Manufacturer"] = inventory.Manufacturer,
                ["querystring__Serial"] = inventory.Serial
            }));
        secondaryResponse.EnsureSuccessStatusCode();
    }

    public bool IsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public async Task RenameComputerAsync(string newComputerName, RenameCredentials? credentials = null)
    {
        var escapedComputerName = EscapePowerShellSingleQuotedString(newComputerName);
        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine("$ErrorActionPreference = 'Stop'");
        if (credentials is not null)
        {
            var escapedUsername = EscapePowerShellSingleQuotedString(credentials.Username);
            var escapedPassword = EscapePowerShellSingleQuotedString(credentials.Password);
            scriptBuilder.AppendLine($"$securePassword = ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force");
            scriptBuilder.AppendLine($"$credential = New-Object System.Management.Automation.PSCredential('{escapedUsername}', $securePassword)");
            scriptBuilder.AppendLine($"$renameParams = @{{ NewName = '{escapedComputerName}'; Force = $true; ErrorAction = 'Stop'; DomainCredential = $credential }}");
        }
        else
        {
            scriptBuilder.AppendLine($"$renameParams = @{{ NewName = '{escapedComputerName}'; Force = $true; ErrorAction = 'Stop' }}");
        }

        scriptBuilder.AppendLine("try {");
        scriptBuilder.AppendLine("    Rename-Computer @renameParams");
        scriptBuilder.AppendLine("} catch {");
        scriptBuilder.AppendLine("    $message = $_.Exception.Message");
        scriptBuilder.AppendLine("    if ([string]::IsNullOrWhiteSpace($message)) {");
        scriptBuilder.AppendLine("        $message = 'Unable to rename the computer.'");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("    Write-Output $message");
        scriptBuilder.AppendLine("    exit 1");
        scriptBuilder.AppendLine("}");
        var script = scriptBuilder.ToString();

        var startInfo = new ProcessStartInfo
        {
            FileName = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(script))}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start PowerShell.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            throw new InvalidOperationException(message.Trim());
        }
    }

    public async Task<LocalAdminResult> AddToLocalAdministratorsAsync(string username)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "net.exe",
            Arguments = $"localgroup administrators \"{username}\" /add",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start net.exe.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        var normalized = output.Trim();

        if (normalized.Contains("already a member", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalAdminResult(false, true, normalized);
        }

        if (process.ExitCode == 0)
        {
            return new LocalAdminResult(true, false, normalized);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(normalized);
        }

        return new LocalAdminResult(true, false, normalized);
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }
}
