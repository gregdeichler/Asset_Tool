$installRoot = "$env:ProgramFiles\Vassar College\Asset Tool"
$exePath = Join-Path $installRoot "AssetTool.exe"

if (-not (Test-Path $exePath)) {
    exit 1
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
if ($version -and $version.StartsWith("1.0.0")) {
    Write-Output "Detected Asset Tool $version"
    exit 0
}

exit 1
