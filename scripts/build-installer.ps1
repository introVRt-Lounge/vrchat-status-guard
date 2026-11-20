#Requires -Version 5.1
<#
.SYNOPSIS
  Publish self-contained win-x64 app and compile Inno Setup wizard.
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$publishDir = Join-Path $RepoRoot "artifacts\publish"
$installerOut = Join-Path $RepoRoot "artifacts\installer"
$appProject = Join-Path $RepoRoot "src\VrchatPrivacy.App\VrchatPrivacy.App.csproj"
$iss = Join-Path $RepoRoot "installer\vrchat-privacy.iss"

Write-Host "Publishing self-contained win-x64 to $publishDir"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $installerOut | Out-Null

dotnet publish $appProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $publishDir "VrchatPrivacy.App.exe"
if (-not (Test-Path $exe)) { throw "Published exe missing: $exe" }

function Find-Iscc {
    $candidates = @(
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 7\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6/7 (winget install JRSoftware.InnoSetup.7)."
}

Write-Host "Compiling installer with $iscc"
& $iscc `
    "/DMyAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerOut" `
    $iss

if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

Get-ChildItem $installerOut -Filter "*.exe" | ForEach-Object {
    Write-Host "Built $($_.FullName) ($([math]::Round($_.Length/1MB,1)) MB)"
}
