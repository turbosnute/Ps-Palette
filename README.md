# Ps-Palette

A PowerShell module for extracting color palettes from images using the Material Design color palette algorithm.

## Features

- Extract vibrant, muted, and dominant colors from any image
- High-performance image processing with automatic downscaling
- Cross-platform support (Windows, macOS, Linux)
- Colored terminal output using ANSI escape codes
- Both integer and hex color format support

## Installation

```powershell
# Clone the repository
git clone https://github.com/yourusername/Ps-Palette.git
cd Ps-Palette

# Build the module
.\build.ps1

# Import the module
Import-Module .\publish\Ps-Palette\
```

## Usage

### Basic Usage
```powershell
# Extract palette from an image
$palette = Get-ImagePalette -Path "photo.jpg"

# Display all colors with ANSI coloring
$palette.ToString()

# Access individual colors
$palette.VibrantColor.HexValue    # "#FFC0F830"
$palette.VibrantColor.IntValue    # -4130768
```

### Performance Tuning
```powershell
# High quality (slower)
Get-ImagePalette -Path "image.jpg" -MaxWidth 400

# Fast processing (lower quality)
Get-ImagePalette -Path "image.jpg" -MaxWidth 50

# Verbose output for debugging
Get-ImagePalette -Path "image.jpg" -MaxWidth 200 -Verbose
```

## Available Colors

- **VibrantColor** - A vibrant color from the image
- **LightVibrantColor** - A vibrant and light color
- **DarkVibrantColor** - A vibrant and dark color
- **MutedColor** - A muted color from the image
- **LightMutedColor** - A muted and light color
- **DarkMutedColor** - A muted and dark color
- **DominantColor** - The most dominant color in the image

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Path` | String | Required | Path to the image file |
| `MaxWidth` | Int | 200 | Maximum width in pixels for image processing (50-2000) |

## Supported Formats

- JPEG (.jpg, .jpeg)
- PNG (.png)
- BMP (.bmp)
- GIF (.gif)
- WEBP (.webp)
- And other formats supported by SkiaSharp

## Requirements

- PowerShell 5.1+ or PowerShell 7+
- .NET Framework