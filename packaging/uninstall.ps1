param(
    [switch]$Quiet,
    [string]$InstallRoot = "$env:ProgramFiles\Vassar College\Asset Tool"
)

$ErrorActionPreference = "Stop"

function Ensure-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        return
    }

    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`"",
        "-Quiet:$Quiet",
        "-InstallRoot", "`"$InstallRoot`""
    )

    $process = Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs -PassThru -Wait
    exit $process.ExitCode
}

Ensure-Administrator

$shortcutPath = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Vassar College\Asset Tool.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

$shortcutFolder = Split-Path $shortcutPath -Parent
if (Test-Path $shortcutFolder -and -not (Get-ChildItem -Path $shortcutFolder -Force | Select-Object -First 1)) {
    Remove-Item -LiteralPath $shortcutFolder -Force
}

if (Test-Path $InstallRoot) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}

$uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AssetTool"
if (Test-Path $uninstallKey) {
    Remove-Item -Path $uninstallKey -Recurse -Force
}

if (-not $Quiet) {
    Write-Host "Asset Tool uninstalled."
}
