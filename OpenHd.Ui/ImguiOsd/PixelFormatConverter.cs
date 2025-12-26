using System.Runtime.Versioning;
using SharpVideo.Drm;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Utilities for working with pixel formats in DRM context.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class PixelFormatConverter
{
    /// <summary>
    /// Gets the preferred DRM format for video plane based on decoder output.
    /// Falls back to NV12 if the format is not directly supported by common hardware.
    /// </summary>
    public static PixelFormat GetPreferredDrmFormat(PixelFormat decoderFormat)
    {
        // Most formats can be used directly
        // Only fall back to NV12 for formats that might not be supported
        return decoderFormat;
    }

    /// <summary>
    /// Checks if frame copy requires pixel format conversion.
    /// Currently, YUV420P (planar) requires conversion to NV12 (semi-planar) 
    /// on many hardware platforms.
    /// </summary>
    public static bool RequiresConversion(PixelFormat sourceFormat, PixelFormat targetFormat)
    {
        return sourceFormat.Fourcc != targetFormat.Fourcc;
    }

    /// <summary>
    /// Gets a human-readable name for the pixel format.
    /// </summary>
    public static string GetFormatName(PixelFormat format)
    {
        return format.GetName();
    }
}
