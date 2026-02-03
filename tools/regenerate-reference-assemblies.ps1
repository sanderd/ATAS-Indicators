# Regenerates reference assemblies from ATAS Platform DLLs using JetBrains Refasmer
# Run this script after ATAS Platform updates to refresh the reference assemblies

$ErrorActionPreference = "Stop"

$AtasPath = "C:\Program Files (x86)\ATAS Platform"
$OutputPath = Join-Path $PSScriptRoot "..\solution\ReferenceAssemblies\generated"

$DllsToGenerate = @(
    "ATAS.Indicators.dll",
    "ATAS.Indicators.Technical.dll",
    "OFT.Attributes.dll",
    "OFT.Localization.dll",
    "OFT.Rendering.dll",
    "Utils.Common.dll"
)

# Check if ATAS Platform is installed
if (-not (Test-Path $AtasPath)) {
    Write-Error "ATAS Platform not found at: $AtasPath"
    exit 1
}

# Check if refasmer is installed
$refasmer = Get-Command refasmer -ErrorAction SilentlyContinue
if (-not $refasmer) {
    Write-Host "Installing JetBrains.Refasmer.CliTool..."
    dotnet tool install JetBrains.Refasmer.CliTool --global
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
}

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Copy DLLs to temp (avoids path issues with spaces)
$TempDir = Join-Path $env:TEMP "atas-refasmer"
if (-not (Test-Path $TempDir)) {
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
}

Write-Host "Copying ATAS DLLs to temp directory..."
foreach ($dll in $DllsToGenerate) {
    $source = Join-Path $AtasPath $dll
    if (Test-Path $source) {
        Copy-Item $source -Destination $TempDir -Force
    } else {
        Write-Warning "DLL not found: $source"
    }
}

# Generate reference assemblies
Write-Host "Generating reference assemblies..."
$dllPaths = $DllsToGenerate | ForEach-Object { Join-Path $TempDir $_ }
& refasmer --all --continue -O $OutputPath @dllPaths

# Clean up temp
Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nReference assemblies generated in: $OutputPath"
Get-ChildItem $OutputPath
