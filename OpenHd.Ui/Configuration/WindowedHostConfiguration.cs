namespace OpenHd.Ui.Configuration;

/// <summary>
/// Configuration for windowed host using SDL3 and OpenGL.
/// </summary>
internal sealed class WindowedHostConfiguration : HostConfigurationBase
{
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public string WindowTitle { get; set; } = "OpenHD UI";
    public bool EnableImGuiDocking { get; set; } = true;
    public bool EnableKeyboardNavigation { get; set; } = true;
    public bool EnableGamepadNavigation { get; set; } = true;
}