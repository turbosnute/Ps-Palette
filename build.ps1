param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\publish\Ps-Palette"
)

Write-Host "Building Ps-Palette module with native dependencies..." -ForegroundColor Green

# Clean previous build
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
    Write-Host "Cleaned previous build" -ForegroundColor Yellow
}

# Restore packages first to ensure native assets are downloaded
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c $Configuration --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    throw "Build failed"
}

# Create output directory
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Copy module manifest
Write-Host "Copying module manifest..." -ForegroundColor Yellow
Copy-Item "Ps-Palette.psd1" $OutputPath

# Get build output directory
$buildOutput = "bin\$Configuration\netstandard2.0"

# Copy main assembly
Write-Host "Copying main assembly..." -ForegroundColor Yellow
Copy-Item "$buildOutput\Ps-Palette.dll" $OutputPath

# Copy all managed dependencies
Write-Host "Copying managed dependencies..." -ForegroundColor Yellow
$dependencies = Get-ChildItem "$buildOutput\*.dll" | Where-Object { 
    $_.Name -ne "Ps-Palette.dll" -and 
    $_.Name -ne "System.Management.Automation.dll" 
}

foreach ($dep in $dependencies) {
    Copy-Item $dep.FullName $OutputPath
    Write-Host "  Copied: $($dep.Name)" -ForegroundColor Gray
}

# Find and copy native SkiaSharp libraries from NuGet packages
Write-Host "Locating SkiaSharp native libraries..." -ForegroundColor Yellow

# Check NuGet global packages folder
$nugetPackages = @(
    "$env:USERPROFILE\.nuget\packages",
    "$env:NUGET_PACKAGES"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($nugetPackages) {
    Write-Host "Searching in NuGet packages: $nugetPackages" -ForegroundColor Gray
    
    # Look for SkiaSharp native assets
    $skiaSharpPaths = @(
        "$nugetPackages\skiasharp\*\runtimes",
        "$nugetPackages\skiasharp.nativeassets.win32\*\runtimes"
    )
    
    foreach ($searchPath in $skiaSharpPaths) {
        $runtimePaths = Get-ChildItem $searchPath -ErrorAction SilentlyContinue
        if ($runtimePaths) {
            Write-Host "Found SkiaSharp runtimes at: $($runtimePaths.FullName)" -ForegroundColor Gray
            Copy-Item $runtimePaths.FullName $OutputPath -Recurse -Force
            break
        }
    }
}

# Also copy from build output if available
$runtimesPath = "$buildOutput\runtimes"
if (Test-Path $runtimesPath) {
    Write-Host "Copying runtimes from build output..." -ForegroundColor Yellow
    Copy-Item $runtimesPath $OutputPath -Recurse -Force
}

# Ensure native libraries are in the root directory (PowerShell prefers this)
Write-Host "Copying native libraries to module root..." -ForegroundColor Yellow
$platform = if ([Environment]::Is64BitProcess) { "win-x64" } else { "win-x86" }
$nativePath = "$OutputPath\runtimes\$platform\native"

if (Test-Path $nativePath) {
    $nativeLibs = Get-ChildItem $nativePath -Filter "*.dll"
    foreach ($lib in $nativeLibs) {
        Copy-Item $lib.FullName $OutputPath
        Write-Host "  Copied to root: $($lib.Name)" -ForegroundColor Gray
    }
} else {
    Write-Warning "Native libraries not found for $platform"
}

# Create a module loader script that sets up the environment
$loaderScript = @"
# Module loader for Ps-Palette
`$ModulePath = `$PSScriptRoot

# Add native library path to PATH for this session
if (`$ModulePath -notin (`$env:PATH -split ';')) {
    `$env:PATH = "`$ModulePath;`$env:PATH"
}

# Load the main module
Import-Module "`$ModulePath\Ps-Palette.dll" -Force
"@

$loaderScript | Out-File "$OutputPath\Ps-Palette.psm1" -Encoding UTF8

# Update the module manifest to use the loader
Write-Host "Updating module manifest..." -ForegroundColor Yellow
$manifestContent = Get-Content "Ps-Palette.psd1" -Raw
$manifestContent = $manifestContent -replace "RootModule = 'Ps-Palette.dll'", "RootModule = 'Ps-Palette.psm1'"
$manifestContent | Out-File "$OutputPath\Ps-Palette.psd1" -Encoding UTF8

# Verify all components
Write-Host "Verifying module components..." -ForegroundColor Yellow
$criticalFiles = @(
    "Ps-Palette.psd1",
    "Ps-Palette.psm1", 
    "Ps-Palette.dll",
    "PaletteNet.dll",
    "SkiaSharp.dll",
    "libSkiaSharp.dll"
)

foreach ($file in $criticalFiles) {
    $filePath = Join-Path $OutputPath $file
    if (Test-Path $filePath) {
        Write-Host "  ✓ $file" -ForegroundColor Green
    } else {
        Write-Warning "  ✗ $file (missing)"
    }
}

Write-Host "Build completed!" -ForegroundColor Green
Write-Host "Module location: $OutputPath" -ForegroundColor Cyan

# Test the module
Write-Host "Testing module..." -ForegroundColor Yellow
try {
    Import-Module "$OutputPath\Ps-Palette.psd1" -Force
    Write-Host "✓ Module imported successfully!" -ForegroundColor Green
    Remove-Module Ps-Palette -ErrorAction SilentlyContinue
} catch {
    Write-Warning "Module import test failed: $($_.Exception.Message)"
}