namespace OpenHd.Ui.Configuration;

/// <summary>
/// Configuration for DRM host using direct KMS rendering.
/// </summary>
internal sealed class DrmHostConfiguration : HostConfigurationBase
{
    public uint DisplayWidth { get; set; } = 1920;
    public uint DisplayHeight { get; set; } = 1080;
    public float UiScale { get; set; } = 1.0f;
    public bool EnableInput { get; set; } = true;
    public string? DrmDevicePath { get; set; } = null;
}
