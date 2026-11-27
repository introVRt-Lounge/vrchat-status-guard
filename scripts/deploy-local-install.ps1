#Requires -Version 5.1
<#
.SYNOPSIS
  Publish the current tree into the Velopack install's current\ folder
  (%LOCALAPPDATA%\StatusGuard\current) and point HKCU Run at StatusGuard.exe.

  Prefer official Setup.exe for real installs. This script is for local dogfood
  and will leave Velopack update metadata alone only if you copy into current\
  without deleting Update.exe / packages\.
#>
param(
    [string]$Configuration = "Release",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "StatusGuard\current"),
    [string]$RunValueName = "StatusGuard",
    [switch]$SkipStart
)

$ErrorActionPreference = "Stop"

$appProject = Join-Path $RepoRoot "src\StatusGuard.App\StatusGuard.App.csproj"
$publishDir = Join-Path $RepoRoot "artifacts\local-install"
$exeName = "StatusGuard.exe"
$installedExe = Join-Path $InstallDir $exeName

if (-not (Test-Path $InstallDir)) {
    throw "Install dir missing: $InstallDir (run the official Setup once first)."
}

Write-Host "Publishing win-x64 self-contained -> $publishDir"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$git = ""
try { $git = (git -C $RepoRoot rev-parse --short HEAD 2>$null).Trim() } catch { }
$infoVersion = if ($git) { "0.1.0+local.$git" } else { "0.1.0+local" }

dotnet publish $appProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=portable `
    -p:Version=0.1.0 `
    -p:InformationalVersion=$infoVersion `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$publishedExe = Join-Path $publishDir $exeName
if (-not (Test-Path $publishedExe)) { throw "Published exe missing: $publishedExe" }

Write-Host "Stopping running Status Guard…"
Get-Process StatusGuard, StatusGuard.App, VrchatPrivacy.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Write-Host "Copying publish -> $InstallDir"
robocopy $publishDir $InstallDir /E /IS /IT /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
$rc = $LASTEXITCODE
# robocopy: 0-7 success-ish
if ($rc -ge 8) { throw "robocopy failed with code $rc" }

$runPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$desired = "`"$installedExe`""
# Drop obsolete Run value names from prior branding.
Remove-ItemProperty -Path $runPath -Name 'VrchatStatusGuard' -ErrorAction SilentlyContinue
$current = (Get-ItemProperty -Path $runPath -Name $RunValueName -ErrorAction SilentlyContinue).$RunValueName
if ($current -ne $desired) {
    Write-Host "Updating HKCU Run\$RunValueName -> $desired"
    Set-ItemProperty -Path $runPath -Name $RunValueName -Value $desired
} else {
    Write-Host "HKCU Run\$RunValueName already points at install exe."
}

$vi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $InstallDir "StatusGuard.dll"))
Write-Host "Installed ProductVersion: $($vi.ProductVersion)"

if (-not $SkipStart) {
    Write-Host "Starting $installedExe"
    Start-Process $installedExe
}

Write-Host "Done. Startup and tray should now match this tree ($infoVersion)."
