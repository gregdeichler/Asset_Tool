param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$releaseRoot = Join-Path $projectRoot "release\$Version"
$appOut = Join-Path $releaseRoot "app"
$appZip = Join-Path $releaseRoot "app.zip"
$installerWork = Join-Path $releaseRoot "installer-work"
$installerOut = Join-Path $releaseRoot "installer"
$sourceOut = Join-Path $releaseRoot "source"
$intuneSource = Join-Path $releaseRoot "intune-source"
$intuneOut = Join-Path $releaseRoot "intune"
$toolingOut = Join-Path $releaseRoot "tooling"
$intermediateOut = Join-Path $projectRoot ".tmpobj\release-$Version\"
$runtimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.5/windowsdesktop-runtime-10.0.5-win-x64.exe"
$runtimeFile = Join-Path $toolingOut "windowsdesktop-runtime-10.0.5-win-x64.exe"
$intuneToolUrl = "https://raw.githubusercontent.com/microsoft/Microsoft-Win32-Content-Prep-Tool/master/IntuneWinAppUtil.exe"
$intuneTool = Join-Path $toolingOut "IntuneWinAppUtil.exe"
$productName = "Asset Tool"
$appArchiveName = "AssetTool-1.0.0-app.zip"
$sourceArchiveName = "AssetTool-1.0.0-source.zip"
$setupName = "AssetTool-1.0.0-Setup.exe"

if (Test-Path $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $releaseRoot,$appOut,$installerWork,$installerOut,$sourceOut,$intuneSource,$intuneOut,$toolingOut,$intermediateOut | Out-Null

$env:APPDATA = Join-Path $projectRoot "AppData\Roaming"
$env:LOCALAPPDATA = Join-Path $projectRoot "AppData\Local"
$env:USERPROFILE = $projectRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet"
$env:NUGET_PACKAGES = Join-Path $projectRoot ".nuget\packages"
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot ".nuget\http-cache"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES,$env:NUGET_HTTP_CACHE_PATH | Out-Null

& "C:\Program Files\dotnet\dotnet.exe" build (Join-Path $projectRoot "ModernAssetTool.App.csproj") -c Release -o $appOut --configfile (Join-Path $projectRoot "NuGet.Config") -p:BaseIntermediateOutputPath=$intermediateOut
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeFile
Invoke-WebRequest -Uri $intuneToolUrl -OutFile $intuneTool

Copy-Item -Path (Join-Path $projectRoot "packaging\install.ps1") -Destination $installerWork -Force
Copy-Item -Path (Join-Path $projectRoot "packaging\uninstall.ps1") -Destination $installerWork -Force
Copy-Item -Path (Join-Path $projectRoot "packaging\detect.ps1") -Destination $installerWork -Force
Copy-Item -Path (Join-Path $projectRoot "packaging\install.cmd") -Destination $installerWork -Force
Copy-Item -Path (Join-Path $projectRoot "packaging\uninstall.cmd") -Destination $installerWork -Force
Copy-Item -Path $runtimeFile -Destination $installerWork -Force
Compress-Archive -Path (Join-Path $appOut "*") -DestinationPath $appZip -Force
Copy-Item -Path $appZip -Destination (Join-Path $installerWork "app.zip") -Force

$sedTemplate = Get-Content -Path (Join-Path $projectRoot "packaging\installer.sed.template") -Raw
$sourceDir = $installerWork
$targetExe = Join-Path $installerOut $setupName
$sedContent = $sedTemplate.Replace("{{SOURCE_DIR}}", $sourceDir).Replace("{{TARGET_EXE}}", $targetExe)
$sedPath = Join-Path $installerOut "installer.sed"
Set-Content -Path $sedPath -Value $sedContent -Encoding ASCII
& "C:\Windows\System32\iexpress.exe" /N $sedPath
if ($LASTEXITCODE -ne 0) {
    throw "IExpress installer build failed."
}

Copy-Item -Path $installerWork\* -Destination $intuneSource -Recurse -Force
Copy-Item -Path (Join-Path $projectRoot "packaging\Intune-Deployment-Notes.txt") -Destination $intuneOut -Force
& $intuneTool -c $intuneSource -s (Join-Path $intuneSource "install.cmd") -o $intuneOut -q
if ($LASTEXITCODE -ne 0) {
    throw "IntuneWinAppUtil failed."
}

$sourceKeep = @(
    "App.xaml",
    "App.xaml.cs",
    "Build-Release.ps1",
    "ModernAssetTool.App.csproj",
    "NuGet.Config",
    "PreferencesWindow.xaml",
    "PreferencesWindow.xaml.cs",
    "README.md",
    "RenameCredentialsWindow.xaml",
    "RenameCredentialsWindow.xaml.cs",
    "SimpleMainWindow.xaml",
    "SimpleMainWindow.xaml.cs",
    "Assets",
    "docs\github-screenshot.png",
    "Models\AppSettings.cs",
    "Models\InventorySnapshot.cs",
    "Models\RenameCredentials.cs",
    "Services\AppSettingsService.cs",
    "Services\InventoryService.cs",
    "Services\WebhookService.cs",
    "packaging"
)

foreach ($item in $sourceKeep) {
    $sourcePath = Join-Path $projectRoot $item
    $destinationPath = Join-Path $sourceOut $item
    $destinationDirectory = Split-Path -Path $destinationPath -Parent
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Copy-Item -Path $sourcePath -Destination $destinationPath -Recurse -Force
}

Compress-Archive -Path (Join-Path $sourceOut "*") -DestinationPath (Join-Path $releaseRoot $sourceArchiveName) -Force
Copy-Item -Path $appZip -Destination (Join-Path $releaseRoot $appArchiveName) -Force

@"
Asset Tool 1.0.0 Final

Artifacts:
- app\ : framework-dependent application files
- installer\$setupName : bootstrap installer
- intune\*.intunewin : Intune Win32 package
- $sourceArchiveName : GitHub-ready source archive
- $appArchiveName : release app archive

Official runtime bundled:
$runtimeUrl

Intune packaging tool source:
https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool
"@ | Set-Content -Path (Join-Path $releaseRoot "RELEASE-NOTES.txt") -Encoding UTF8

Write-Host "Release built at $releaseRoot"
