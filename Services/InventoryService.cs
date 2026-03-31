using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModernAssetTool.App.Models;

namespace ModernAssetTool.App.Services;

public sealed class InventoryService
{
    private const string PowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    public async Task<InventorySnapshot> GetInventoryAsync()
    {
        var now = DateTime.Now;
        var fallback = new InventorySnapshot
        {
            ComputerName = Environment.MachineName,
            Memory = "Unavailable",
            Date = now.ToString("M/d/yyyy"),
            Time = now.ToString("HH:mm:ss")
        };

        try
        {
            var script = """
                $ErrorActionPreference = 'Stop'
                function Get-FormFactor {
                    try {
                        $code = [int]((Get-CimInstance Win32_SystemEnclosure).ChassisTypes[0])
                        switch ($code) {
                            0 { 'Other' }
                            1 { 'Unknown' }
                            3 { 'Desktop' }
                            4 { 'Low Profile Desktop' }
                            5 { 'Pizza Box' }
                            6 { 'Mini Tower' }
                            7 { 'Tower' }
                            8 { 'Portable' }
                            9 { 'Laptop' }
                            10 { 'Notebook' }
                            13 { 'All-in-one' }
                            23 { 'Rack mount chassis' }
                            default { 'Undefined' }
                        }
                    } catch {
                        'Undefined'
                    }
                }
                $cs = Get-CimInstance Win32_ComputerSystem
                $os = Get-CimInstance Win32_OperatingSystem
                $bios = Get-CimInstance Win32_BIOS
                $prod = Get-CimInstance Win32_ComputerSystemProduct
                [pscustomobject]@{
                    ComputerName = $cs.Name
                    Manufacturer = $cs.Manufacturer
                    Model = $cs.Model
                    OSCaption = $os.Caption
                    OSVersion = $os.Version
                    Architecture = $os.OSArchitecture
                    Serial = $bios.SerialNumber
                    UUID = $prod.UUID
                    FormFactor = Get-FormFactor
                    Memory = if ($cs.TotalPhysicalMemory) { '{0:N0} GB' -f [math]::Round($cs.TotalPhysicalMemory / 1GB) } else { 'Unavailable' }
                    IsDomainJoined = [bool]$cs.PartOfDomain
                    DomainName = if ($cs.PartOfDomain) { $cs.Domain } else { '' }
                    Date = (Get-Date -Format 'M/d/yyyy')
                    Time = (Get-Date -Format 'HH:mm:ss')
                } | ConvertTo-Json -Compress
                """;

            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var psi = new ProcessStartInfo
            {
                FileName = PowerShellPath,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start PowerShell.");
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return fallback;
            }

            var snapshot = JsonSerializer.Deserialize<InventorySnapshot>(stdout);
            if (snapshot is null)
            {
                return fallback;
            }

            snapshot.ComputerName = string.IsNullOrWhiteSpace(snapshot.ComputerName) ? fallback.ComputerName : snapshot.ComputerName;
            snapshot.Memory = string.IsNullOrWhiteSpace(snapshot.Memory) ? fallback.Memory : snapshot.Memory;
            snapshot.DomainName = string.IsNullOrWhiteSpace(snapshot.DomainName) ? string.Empty : snapshot.DomainName;
            snapshot.Date = string.IsNullOrWhiteSpace(snapshot.Date) ? fallback.Date : snapshot.Date;
            snapshot.Time = string.IsNullOrWhiteSpace(snapshot.Time) ? fallback.Time : snapshot.Time;
            return snapshot;
        }
        catch
        {
            return fallback;
        }
    }
}
