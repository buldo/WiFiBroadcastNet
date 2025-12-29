namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Configuration for windowed host using SDL3 and OpenGL.
/// </summary>
internal sealed class WindowedHostConfiguration
{
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public string WindowTitle { get; set; } = "OpenHD UI";
    public bool ShowDemoWindow { get; set; } = true;
    public bool EnableImGuiDocking { get; set; } = true;
    public bool EnableKeyboardNavigation { get; set; } = true;
    public bool EnableGamepadNavigation { get; set; } = true;
}

/// <summary>
/// Configuration for DRM host using direct KMS rendering.
/// </summary>
internal sealed class DrmHostConfiguration
{
    public uint DisplayWidth { get; set; } = 1920;
    public uint DisplayHeight { get; set; } = 1080;
    public bool ShowDemoWindow { get; set; } = true;
    public float UiScale { get; set; } = 1.0f;
    public bool EnableInput { get; set; } = true;
    public string? DrmDevicePath { get; set; } = null;
    public uint VideoPlaneZOrder { get; set; } = 0;
    public uint UiPlaneZOrder { get; set; } = 1;
}
