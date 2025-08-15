using System;
using System.Management.Automation;
using PaletteNet;
using SkiaSharp; // Use the cross-platform library

// Add this class to provide both int and hex representations
public class ColorInfo
{
    public int? IntValue { get; set; }
    public string HexValue { get; set; }

    public ColorInfo(int? colorInt)
    {
        IntValue = colorInt;
        HexValue = colorInt.HasValue ? $"#{(uint)colorInt.Value:X8}" : null;
    }

    public override string ToString()
    {
        if (!IntValue.HasValue)
            return "null";

        // Extract RGB values from ARGB integer
        uint argb = (uint)IntValue.Value;
        int r = (int)((argb >> 16) & 0xFF);
        int g = (int)((argb >> 8) & 0xFF);
        int b = (int)(argb & 0xFF);

        // Create ANSI escape code for the color
        string colorCode = $"\u001b[38;2;{r};{g};{b}m";
        string resetCode = "\u001b[0m";

        return $"{colorCode}{HexValue}{resetCode}";
    }
}

public class EnhancedPalette
{
    public ColorInfo VibrantColor { get; set; }
    public ColorInfo LightVibrantColor { get; set; }
    public ColorInfo DarkVibrantColor { get; set; }
    public ColorInfo MutedColor { get; set; }
    public ColorInfo LightMutedColor { get; set; }
    public ColorInfo DarkMutedColor { get; set; }
    public ColorInfo DominantColor { get; set; }

    public EnhancedPalette(Palette palette)
    {
        VibrantColor = new ColorInfo(palette.VibrantColor);
        LightVibrantColor = new ColorInfo(palette.LightVibrantColor);
        DarkVibrantColor = new ColorInfo(palette.DarkVibrantColor);
        MutedColor = new ColorInfo(palette.MutedColor);
        LightMutedColor = new ColorInfo(palette.LightMutedColor);
        DarkMutedColor = new ColorInfo(palette.DarkMutedColor);
        DominantColor = new ColorInfo(palette.DominantColor);
    }

    public override string ToString()
    {
        return $@"VibrantColor: {VibrantColor}
LightVibrantColor: {LightVibrantColor}
DarkVibrantColor: {DarkVibrantColor}
MutedColor: {MutedColor}
LightMutedColor: {LightMutedColor}
DarkMutedColor: {DarkMutedColor}
DominantColor: {DominantColor}";
    }
}

// Define a custom IBitmapHelper that uses SkiaSharp
public class SkiaBitmapHelper : IBitmapHelper, IDisposable
{
    private readonly SKBitmap _originalBitmap;
    private readonly PSCmdlet _cmdlet;
    private readonly int _maxWidth;

    public SkiaBitmapHelper(string imagePath, int maxWidth = 200, PSCmdlet cmdlet = null)
    {
        _cmdlet = cmdlet;
        _maxWidth = maxWidth;
        _cmdlet?.WriteVerbose($"Loading image from path: {imagePath}");
        
        _originalBitmap = SKBitmap.Decode(imagePath);
        if (_originalBitmap == null)
        {
            throw new System.ArgumentException($"Unable to decode image at path: {imagePath}");
        }
        
        _cmdlet?.WriteVerbose($"Image loaded successfully. Dimensions: {_originalBitmap.Width}x{_originalBitmap.Height}, ColorType: {_originalBitmap.ColorType}");
    }

    public int[] ScaleDownAndGetPixels()
    {
        _cmdlet?.WriteVerbose($"Starting image processing. Original size: {_originalBitmap.Width}x{_originalBitmap.Height}, Target max width: {_maxWidth}");
        
        // Calculate scale factor based on width
        var scale = (float)_maxWidth / _originalBitmap.Width;
        
        _cmdlet?.WriteVerbose($"Scale factor: {scale:F3}");
        
        // If image is already small enough, use original
        if (scale >= 1.0f)
        {
            _cmdlet?.WriteVerbose("Image width is already smaller than or equal to target width, using original size");
            var originalPixels = GetPixelsFromBitmap(_originalBitmap);
            _cmdlet?.WriteVerbose($"Extracted {originalPixels.Length} pixels from original image");
            return originalPixels;
        }

        // Calculate new dimensions (width specified, height calculated proportionally)
        var newWidth = _maxWidth;
        var newHeight = (int)(_originalBitmap.Height * scale);
        
        _cmdlet?.WriteVerbose($"Scaling image to {newWidth}x{newHeight} (reduction from {_originalBitmap.Width * _originalBitmap.Height} to {newWidth * newHeight} pixels)");

        // Create scaled bitmap using modern SkiaSharp API
        using (var scaledBitmap = _originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default))
        {
            if (scaledBitmap == null)
            {
                _cmdlet?.WriteVerbose("Scaling failed, falling back to original image");
                var fallbackPixels = GetPixelsFromBitmap(_originalBitmap);
                _cmdlet?.WriteVerbose($"Extracted {fallbackPixels.Length} pixels from fallback original image");
                return fallbackPixels;
            }
            
            _cmdlet?.WriteVerbose($"Image scaled successfully to {scaledBitmap.Width}x{scaledBitmap.Height}");
            var scaledPixels = GetPixelsFromBitmap(scaledBitmap);
            _cmdlet?.WriteVerbose($"Extracted {scaledPixels.Length} pixels from scaled image");
            return scaledPixels;
        }
    }

    private int[] GetPixelsFromBitmap(SKBitmap bitmap)
    {
        _cmdlet?.WriteVerbose($"Converting bitmap to pixel array. Bitmap size: {bitmap.Width}x{bitmap.Height}");
        
        var pixels = new int[bitmap.Width * bitmap.Height];
        for (int i = 0; i < bitmap.Pixels.Length; i++)
        {
            // The (uint) cast gets the 32-bit ARGB value, which is what PaletteNet expects.
            pixels[i] = (int)(uint)bitmap.Pixels[i];
        }
        
        _cmdlet?.WriteVerbose($"Pixel conversion complete. Total pixels: {pixels.Length}");
        return pixels;
    }

    // Implement IDisposable to properly clean up resources
    public void Dispose()
    {
        _cmdlet?.WriteVerbose("Disposing bitmap resources");
        _originalBitmap?.Dispose();
    }
}

// This is the definition of your PowerShell command
[Cmdlet(VerbsCommon.Get, "ImagePalette")]
[OutputType(typeof(EnhancedPalette))]
public class GetImagePaletteCmdlet : PSCmdlet
{
    // Define a parameter that the user will provide, e.g., -Path "..."
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true)]
    public string Path { get; set; }

    // Parameter to control the maximum width for downscaling
    [Parameter(
        Mandatory = false,
        Position = 1)]
    [ValidateRange(50, 2000)]
    public int MaxWidth { get; set; } = 200;

    // This is the main processing method of the cmdlet
    protected override void ProcessRecord()
    {
        base.ProcessRecord();

        WriteVerbose($"Starting palette generation for path: {Path}");
        WriteVerbose($"Using MaxWidth: {MaxWidth} pixels");

        // Resolve the relative path to a full path
        string resolvedPath = GetUnresolvedProviderPathFromPSPath(Path);
        WriteVerbose($"Resolved path: {resolvedPath}");

        // Check if file exists
        if (!System.IO.File.Exists(resolvedPath))
        {
            WriteError(new ErrorRecord(
                new System.IO.FileNotFoundException($"File not found: {resolvedPath}"),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                resolvedPath));
            return;
        }

        WriteVerbose("File exists, proceeding with palette generation");

        try
        {
            // All the complex logic is now hidden inside your C# code
            using (var bitmapHelper = new SkiaBitmapHelper(resolvedPath, MaxWidth, this))
            {
                WriteVerbose("Creating palette builder");
                var paletteBuilder = new PaletteBuilder();
                
                WriteVerbose("Generating palette from image");
                var palette = paletteBuilder.Generate(bitmapHelper);
                
                WriteVerbose("Creating enhanced palette with hex colors");
                var enhancedPalette = new EnhancedPalette(palette);
                
                WriteVerbose("Palette generation complete");
                
                // Write the enhanced palette object to the PowerShell pipeline
                WriteObject(enhancedPalette);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(
                ex,
                "PaletteGenerationError",
                ErrorCategory.InvalidOperation,
                resolvedPath));
        }
    }
}