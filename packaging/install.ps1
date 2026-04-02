param(
    [string]$InstallRoot = "$env:ProgramFiles\Vassar College\Asset Tool",
    [string]$Version = "1.0.0",
    [switch]$LaunchApp = $true
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
        "-InstallRoot", "`"$InstallRoot`"",
        "-Version", "`"$Version`""
    )

    $process = Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs -PassThru -Wait
    exit $process.ExitCode
}

function Test-DotNetDesktopRuntime10 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $runtimes = & $dotnet.Source --list-runtimes 2>$null
        if ($LASTEXITCODE -eq 0 -and ($runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 10\.' })) {
            return $true
        }
    }

    $registryPath = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (Test-Path $registryPath) {
        $versions = (Get-ChildItem $registryPath -ErrorAction SilentlyContinue).PSChildName
        if ($versions | Where-Object { $_ -match '^10\.' }) {
            return $true
        }
    }

    return $false
}

function Ensure-DotNetDesktopRuntime10 {
    if (Test-DotNetDesktopRuntime10) {
        return
    }

    $runtimeInstaller = Join-Path $PSScriptRoot "windowsdesktop-runtime-10.0.5-win-x64.exe"
    if (-not (Test-Path $runtimeInstaller)) {
        throw ".NET Desktop Runtime 10 installer is missing: $runtimeInstaller"
    }

    $process = Start-Process -FilePath $runtimeInstaller -ArgumentList "/install", "/quiet", "/norestart" -PassThru -Wait
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        throw ".NET Desktop Runtime installation failed with exit code $($process.ExitCode)."
    }

    if (-not (Test-DotNetDesktopRuntime10)) {
        throw ".NET Desktop Runtime 10 still was not detected after installation."
    }
}

function New-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$Description
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = Split-Path -Path $TargetPath -Parent
    $shortcut.Description = $Description
    $shortcut.Save()
}

Ensure-Administrator
Ensure-DotNetDesktopRuntime10

$appArchive = Join-Path $PSScriptRoot "app.zip"
if (-not (Test-Path $appArchive)) {
    throw "Application payload archive is missing: $appArchive"
}

if (Test-Path $InstallRoot) {
    Get-ChildItem -Path $InstallRoot -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
}

Expand-Archive -Path $appArchive -DestinationPath $InstallRoot -Force
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.ps1") -Destination (Join-Path $InstallRoot "uninstall.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "detect.ps1") -Destination (Join-Path $InstallRoot "detect.ps1") -Force

$programsFolder = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Vassar College"
New-Item -ItemType Directory -Force -Path $programsFolder | Out-Null
New-Shortcut -ShortcutPath (Join-Path $programsFolder "Asset Tool.lnk") -TargetPath (Join-Path $InstallRoot "AssetTool.exe") -Description "Asset Tool"

$uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AssetTool"
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name DisplayName -Value "Asset Tool"
Set-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value $Version
Set-ItemProperty -Path $uninstallKey -Name Publisher -Value "Vassar College CIS"
Set-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $InstallRoot
Set-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value (Join-Path $InstallRoot "AssetTool.exe")
Set-ItemProperty -Path $uninstallKey -Name UninstallString -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallRoot\uninstall.ps1`""
Set-ItemProperty -Path $uninstallKey -Name QuietUninstallString -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallRoot\uninstall.ps1`" -Quiet"
Set-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -Type DWord

if ($LaunchApp) {
    $appExe = Join-Path $InstallRoot "AssetTool.exe"
    if (Test-Path $appExe) {
        Start-Process -FilePath $appExe -WorkingDirectory $InstallRoot | Out-Null
    }
}

Write-Host "Asset Tool $Version installed to $InstallRoot"
