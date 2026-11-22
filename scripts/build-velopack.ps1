#Requires -Version 5.1
<#
.SYNOPSIS
  Publish self-contained win-x64 app and pack a single Velopack Setup.exe (+ update assets).
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$PackId = "VRChatStatusGuard",
    [string]$VpkVersion = "1.2.161",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ReleaseNotesPath = "",
    [string]$IconPath = "",
    [switch]$SkipToolInstall
)

$ErrorActionPreference = "Stop"

$publishDir = Join-Path $RepoRoot "artifacts\publish"
$velopackOut = Join-Path $RepoRoot "artifacts\velopack"
$appProject = Join-Path $RepoRoot "src\VrchatPrivacy.App\VrchatPrivacy.App.csproj"

Write-Host "Publishing self-contained win-x64 to $publishDir"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $velopackOut | Out-Null

dotnet publish $appProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $publishDir "VrchatPrivacy.App.exe"
if (-not (Test-Path $exe)) { throw "Published exe missing: $exe" }

if (-not $SkipToolInstall) {
    Write-Host "Ensuring vpk $VpkVersion"
    dotnet tool update -g vpk --version $VpkVersion
    if ($LASTEXITCODE -ne 0) {
        dotnet tool install -g vpk --version $VpkVersion
        if ($LASTEXITCODE -ne 0) { throw "vpk tool install failed ($LASTEXITCODE)" }
    }
}

if (-not $IconPath) {
    $candidate = Join-Path $publishDir "Assets\status-guard-logo.png"
    if (Test-Path $candidate) { $IconPath = $candidate }
}

Write-Host "Velopack pack $PackId $Version"
$packArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $Version,
    "--packDir", $publishDir,
    "--mainExe", "VrchatPrivacy.App.exe",
    "--packTitle", "Status Guard",
    "--runtime", "win-x64",
    "--outputDir", $velopackOut
)
if ($ReleaseNotesPath -and (Test-Path $ReleaseNotesPath)) {
    $packArgs += @("--releaseNotes", $ReleaseNotesPath)
}
if ($IconPath -and (Test-Path $IconPath)) {
    # vpk prefers .ico; PNG is accepted on recent vpk for pack icon in some builds - skip if not ico
    if ($IconPath.ToLowerInvariant().EndsWith(".ico")) {
        $packArgs += @("--icon", $IconPath)
    }
}
vpk @packArgs

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }

Get-ChildItem $velopackOut | ForEach-Object {
    Write-Host "Built $($_.FullName) ($([math]::Round($_.Length/1MB,1)) MB)"
}

