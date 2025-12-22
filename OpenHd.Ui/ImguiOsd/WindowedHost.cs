using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.OpenGL;
using Hexa.NET.SDL3;
using SharpVideo.Decoding;
using SDLEvent = Hexa.NET.SDL3.SDLEvent;
using SDLWindow = Hexa.NET.SDL3.SDLWindow;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// When we run application as desktop application, we use SDL3 with OpenGL3
/// </summary>
internal sealed class WindowedHost : UiHostBase
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<WindowedHost> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private Task? _drawThread;
    private Task? _frameFetchThread;

    private FfmpegGlRenderer? _glRenderer;
    private FfmpegDecodedFrame? _currentFrame;
    private readonly object _frameLock = new();

    private int _lastFrameWidth;
    private int _lastFrameHeight;
    private int _lastFrameFormat;
    private long _lastFramePts;
    private bool _lastFrameIsKey;

    public WindowedHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILoggerFactory loggerFactory,
        ILogger<WindowedHost> logger)
        : base(h264Stream, decodersFactory, logger)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _logger.LogInformation("WindowedHost initialized");
    }

    protected override void Start()
    {
        _logger.LogInformation("Starting WindowedHost draw thread");
        _drawThread = Task.Factory.StartNew(DrawThread, TaskCreationOptions.LongRunning);
        _frameFetchThread = Task.Factory.StartNew(FrameFetchingLoop, TaskCreationOptions.LongRunning);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WindowedHost");
        await _cancellationTokenSource.CancelAsync();

        if (_drawThread != null)
        {
            await _drawThread;
        }

        if (_frameFetchThread != null)
        {
            await _frameFetchThread;
        }

        lock (_frameLock)
        {
            if (_currentFrame != null)
            {
                H264Decoder.ReuseDecodedFrame(_currentFrame);
                _currentFrame = null;
            }
        }
    }

    private void FrameFetchingLoop()
    {
        _logger.LogInformation("Starting frame fetching loop");
        int frameCount = 0;

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.LogTrace("Waiting for decoded frame #{Count}", frameCount + 1);

                var decodedFrame = H264Decoder.WaitForDecodedFrames();

                _logger.LogTrace("Received decoded frame #{Count}", frameCount + 1);

                if (decodedFrame is FfmpegDecodedFrame ffmpegFrame)
                {
                    FfmpegDecodedFrame? frameToReturn = null;

                    lock (_frameLock)
                    {
                        if (_currentFrame != null)
                        {
                            _logger.LogWarning("Overwriting unrendered frame! Frame #{Count} - returning old frame", frameCount);
                            frameToReturn = _currentFrame;
                        }
                        _currentFrame = ffmpegFrame;
                        _logger.LogTrace("Frame #{Count} ready for rendering", frameCount + 1);
                    }

                    if (frameToReturn != null)
                    {
                        _logger.LogTrace("Returning overwritten frame to decoder");
                        H264Decoder.ReuseDecodedFrame(frameToReturn);
                    }

                    frameCount++;

                    if (frameCount % 30 == 0)
                    {
                        _logger.LogDebug("Fetched {Count} frames so far", frameCount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in frame fetching loop after {Count} frames", frameCount);
        }
        finally
        {
            _logger.LogInformation("Exiting frame fetching loop, fetched {Count} frames", frameCount);
        }
    }

    private unsafe void DrawThread()
    {
        GL? GL = null;
        SDLGLContext context = SDLGLContext.Null;
        SDLWindow* window = null;
        try
        {
            _logger.LogInformation("DrawThread started");

            // Initialize SDL and create window in the same thread that will handle events
            SDL.SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
            SDL.Init(SDLInitFlags.Events | SDLInitFlags.Video);

            float mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay());
            _logger.LogInformation("Display scale: {Scale}", mainScale);

            window = SDL.CreateWindow("Test Window", (int)(1280 * mainScale), (int)(720 * mainScale),
                SDLWindowFlags.Resizable | SDLWindowFlags.Opengl | SDLWindowFlags.HighPixelDensity);
            var windowId = SDL.GetWindowID(window);
            _logger.LogInformation("Window created. Window ID: {WindowId}", windowId);

            _logger.LogInformation("Creating GL context");
            context = SDL.GLCreateContext(window);

            if (context.Handle == 0)
            {
                _logger.LogError("Failed to create GL context");
                return;
            }

            _logger.LogInformation("Creating ImGui context");
            var guiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(guiContext);

            // Setup ImGui config.
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            var style = ImGui.GetStyle();
            style.ScaleAllSizes(mainScale);
            style.FontScaleDpi = mainScale;
            io.ConfigDpiScaleFonts = true;
            io.ConfigDpiScaleViewports = true;

            _logger.LogInformation("Initializing ImGui SDL3 backend");
            ImGuiImplSDL3.SetCurrentContext(guiContext);
            if (!ImGuiImplSDL3.InitForOpenGL(new SDLWindowPtr((Hexa.NET.ImGui.Backends.SDL3.SDLWindow*)window), (void*)context.Handle))
            {
                _logger.LogError("Failed to init ImGui Impl SDL3");
                return;
            }

            _logger.LogInformation("Initializing ImGui OpenGL3 backend");
            ImGuiImplOpenGL3.SetCurrentContext(guiContext);
            if (!ImGuiImplOpenGL3.Init((byte*)null))
            {
                _logger.LogError("Failed to init ImGui Impl OpenGL3");
                return;
            }

            _logger.LogInformation("Creating GL bindings");
            GL = new GL(new BindingsContext(window, context));

            _logger.LogInformation("Creating video renderer");
            _glRenderer = new FfmpegGlRenderer(GL, _loggerFactory.CreateLogger<FfmpegGlRenderer>());

            _logger.LogInformation("Entering render loop");
            SDLEvent sdlEvent = default;
            bool exiting = false;

            while (!exiting && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                SDL.PumpEvents();

                while (SDL.PollEvent(ref sdlEvent))
                {
                    ImGuiImplSDL3.ProcessEvent((Hexa.NET.ImGui.Backends.SDL3.SDLEvent*)&sdlEvent);

                    switch ((SDLEventType)sdlEvent.Type)
                    {
                        case SDLEventType.Quit:
                            _logger.LogInformation("Received Quit event");
                            exiting = true;
                            break;

                        case SDLEventType.Terminating:
                            _logger.LogInformation("Received Terminating event");
                            exiting = true;
                            break;

                        case SDLEventType.WindowCloseRequested:
                            var windowEvent = sdlEvent.Window;
                            if (windowEvent.WindowID == windowId)
                            {
                                _logger.LogInformation("Received WindowCloseRequested event");
                                exiting = true;
                            }
                            break;
                    }
                }

                GL.MakeCurrent();
                GL.ClearColor(0.1f, 0.1f, 0.1f, 1);
                GL.Clear(GLClearBufferMask.ColorBufferBit);

                FfmpegDecodedFrame? frameToReturn = null;
                lock (_frameLock)
                {
                    if (_currentFrame != null)
                    {
                        _logger.LogTrace("Uploading frame to OpenGL");
                        _glRenderer!.UploadFrame(_currentFrame);

                        var frame = _currentFrame.Frame;
                        _lastFrameWidth = frame->width;
                        _lastFrameHeight = frame->height;
                        _lastFrameFormat = frame->format;
                        _lastFramePts = frame->pts;
                        _lastFrameIsKey = (frame->flags & FFmpeg.AutoGen.ffmpeg.AV_FRAME_FLAG_KEY) != 0;

                        frameToReturn = _currentFrame;
                        _currentFrame = null;
                        _logger.LogTrace("Frame marked for return to decoder");
                    }
                }

                if (_glRenderer != null && _lastFrameWidth > 0)
                {
                    _glRenderer.Render();
                }

                if (frameToReturn != null)
                {
                    _logger.LogTrace("Returning frame to decoder");
                    H264Decoder.ReuseDecodedFrame(frameToReturn);
                    _logger.LogTrace("Frame returned to decoder");
                }

                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplSDL3.NewFrame();
                ImGui.NewFrame();

                ImGui.ShowDemoWindow();
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiCond.FirstUseEver);

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

                ImGui.Render();
                ImGui.EndFrame();

                GL.MakeCurrent();
                ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

                GL.MakeCurrent();
                GL.SwapBuffers();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DrawThread");
        }
        finally
        {
            _logger.LogInformation("Cleaning up resources");

            _glRenderer?.Dispose();

            ImGuiImplOpenGL3.Shutdown();
            ImGuiImplSDL3.Shutdown();
            ImGui.DestroyContext();
            GL?.Dispose();

            if (context != SDLGLContext.Null)
            {
                SDL.GLDestroyContext(context);
            }

            if (window != null)
            {
                SDL.DestroyWindow(window);
            }

            SDL.Quit();

            _logger.LogInformation("DrawThread finished");
        }
    }
}