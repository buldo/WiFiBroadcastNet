using SharpVideo.Decoding;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Represents a video renderer that can render decoded frames.
/// Abstracts the rendering implementation (OpenGL, DRM, etc.).
/// </summary>
internal interface IVideoRenderer : IDisposable
{
    /// <summary>
    /// Uploads a decoded frame to the renderer's buffer.
    /// </summary>
    void UploadFrame(FfmpegDecodedFrame frame);

    /// <summary>
    /// Renders the currently uploaded frame to the output.
    /// </summary>
    void Render();

    /// <summary>
    /// Gets whether a frame has been uploaded and is ready to render.
    /// </summary>
    bool HasFrame { get; }
}
