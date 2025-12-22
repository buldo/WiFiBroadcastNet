using System.Numerics;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Handles ImGui UI rendering logic, independent of the rendering backend (SDL/DRM).
/// Provides a callback-based approach for rendering UI content.
/// </summary>
internal sealed class ImGuiUiRenderer
{
    private readonly ILogger<ImGuiUiRenderer> _logger;
    private readonly Action<float>? _customRenderCallback;
    private readonly bool _showDemoWindow;
    
    private int _lastFrameWidth;
    private int _lastFrameHeight;
    private int _lastFrameFormat;
    private long _lastFramePts;
    private bool _lastFrameIsKey;

    public ImGuiUiRenderer(
        ILogger<ImGuiUiRenderer> logger,
        Action<float>? customRenderCallback = null,
        bool showDemoWindow = true)
    {
        _logger = logger;
        _customRenderCallback = customRenderCallback;
        _showDemoWindow = showDemoWindow;
    }

    /// <summary>
    /// Updates frame statistics to be displayed in the UI.
    /// </summary>
    public void UpdateFrameStatistics(int width, int height, int format, long pts, bool isKeyFrame)
    {
        _lastFrameWidth = width;
        _lastFrameHeight = height;
        _lastFrameFormat = format;
        _lastFramePts = pts;
        _lastFrameIsKey = isKeyFrame;
    }

    /// <summary>
    /// Renders ImGui UI content. Should be called between ImGui.NewFrame() and ImGui.Render().
    /// </summary>
    /// <param name="deltaTime">Time since last frame in seconds</param>
    public void RenderUi(float deltaTime)
    {
        if (_showDemoWindow)
        {
            ImGui.ShowDemoWindow();
        }

        RenderStatisticsWindow();

        _customRenderCallback?.Invoke(deltaTime);
    }

    private void RenderStatisticsWindow()
    {
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 200), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Decoder Statistics"))
        {
            if (_lastFrameWidth > 0)
            {
                ImGui.Text($"Resolution: {_lastFrameWidth}x{_lastFrameHeight}");
                ImGui.Text($"Format: {_lastFrameFormat}");
                ImGui.Text($"PTS: {_lastFramePts}");
                ImGui.Text($"Key Frame: {_lastFrameIsKey}");
            }
            else
            {
                ImGui.Text("Waiting for frames...");
            }
        }
        ImGui.End();
    }
}
