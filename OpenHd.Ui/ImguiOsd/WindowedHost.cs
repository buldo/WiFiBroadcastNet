using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.OpenGL;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;
using SharpVideo.Rtp;
using SDLWindow = Hexa.NET.SDL3.SDLWindow;
using SDLEvent = Hexa.NET.SDL3.SDLEvent;
using SDL = Hexa.NET.SDL3.SDL;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// When we run application as desktop application, we use SDL3 with OpenGL3
/// </summary>
internal sealed class WindowedHost : UiHostBase
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<WindowedHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WindowedHostConfiguration _configuration;

    private Task? _drawThread;
    private VideoFrameManager? _videoFrameManager;
    private ImGuiUiRenderer? _uiRenderer;

    private FfmpegGlRenderer? _glRenderer;

    public WindowedHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILoggerFactory loggerFactory,
        ILogger<WindowedHost> logger,
        WindowedHostConfiguration? configuration = null)
        : base(h264Stream, decodersFactory, logger)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration ?? new WindowedHostConfiguration();
        _logger.LogInformation("WindowedHost initialized");
    }

    protected override void Start()
    {
        _logger.LogInformation("Starting WindowedHost");
        
        _videoFrameManager = new VideoFrameManager(
            H264Decoder, 
            _loggerFactory.CreateLogger<VideoFrameManager>());
        
        _uiRenderer = new ImGuiUiRenderer(
            _loggerFactory.CreateLogger<ImGuiUiRenderer>(),
            customRenderCallback: null,
            showDemoWindow: _configuration.ShowDemoWindow);

        _drawThread = Task.Factory.StartNew(DrawThread, TaskCreationOptions.LongRunning);
        _videoFrameManager.Start();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WindowedHost");
        await _cancellationTokenSource.CancelAsync();

        if (_drawThread != null)
        {
            await _drawThread;
        }

        if (_videoFrameManager != null)
        {
            await _videoFrameManager.StopAsync();
            _videoFrameManager.Dispose();
        }
    }

    private unsafe void DrawThread()
    {
        GL? gl = null;
        Hexa.NET.SDL3.SDLGLContext context = Hexa.NET.SDL3.SDLGLContext.Null;
        SDLWindow* window = null;
        try
        {
            _logger.LogInformation("DrawThread started");

            // Initialize SDL and create window in the same thread that will handle events
            SDL.SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
            SDL.Init(Hexa.NET.SDL3.SDLInitFlags.Events | Hexa.NET.SDL3.SDLInitFlags.Video);

            float mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay());
            _logger.LogInformation("Display scale: {Scale}", mainScale);

            window = SDL.CreateWindow(
                _configuration.WindowTitle, 
                (int)(_configuration.WindowWidth * mainScale), 
                (int)(_configuration.WindowHeight * mainScale),
                Hexa.NET.SDL3.SDLWindowFlags.Resizable | Hexa.NET.SDL3.SDLWindowFlags.Opengl | Hexa.NET.SDL3.SDLWindowFlags.HighPixelDensity);
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
            if (_configuration.EnableKeyboardNavigation)
            {
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            }
            if (_configuration.EnableGamepadNavigation)
            {
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            }
            if (_configuration.EnableImGuiDocking)
            {
                io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            }

            var style = ImGui.GetStyle();
            style.ScaleAllSizes(mainScale);
            style.FontScaleDpi = mainScale;
            io.ConfigDpiScaleFonts = true;
            io.ConfigDpiScaleViewports = true;

            _logger.LogInformation("Initializing ImGui SDL3 backend");
            ImGuiImplSDL3.SetCurrentContext(guiContext);
            if (!ImGuiImplSDL3.InitForOpenGL(new Hexa.NET.ImGui.Backends.SDL3.SDLWindowPtr((Hexa.NET.ImGui.Backends.SDL3.SDLWindow*)window), (void*)context.Handle))
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
            gl = new GL(new BindingsContext(window, context));

            _logger.LogInformation("Creating video renderer");
            _glRenderer = new FfmpegGlRenderer(gl, _loggerFactory.CreateLogger<FfmpegGlRenderer>());

            _logger.LogInformation("Entering render loop");
            RunRenderLoop(window, windowId, gl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DrawThread");
        }
        finally
        {
            CleanupResources(gl, context, window);
            _logger.LogInformation("DrawThread finished");
        }
    }

    private unsafe void RunRenderLoop(SDLWindow* window, uint windowId, GL gl)
    {
        SDLEvent sdlEvent = default;
        bool exiting = false;

        while (!exiting && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            SDL.PumpEvents();

            while (SDL.PollEvent(ref sdlEvent))
            {
                ImGuiImplSDL3.ProcessEvent((Hexa.NET.ImGui.Backends.SDL3.SDLEvent*)&sdlEvent);

                switch ((Hexa.NET.SDL3.SDLEventType)sdlEvent.Type)
                {
                    case Hexa.NET.SDL3.SDLEventType.Quit:
                        _logger.LogInformation("Received Quit event");
                        exiting = true;
                        break;

                    case Hexa.NET.SDL3.SDLEventType.Terminating:
                        _logger.LogInformation("Received Terminating event");
                        exiting = true;
                        break;

                    case Hexa.NET.SDL3.SDLEventType.WindowCloseRequested:
                        var windowEvent = sdlEvent.Window;
                        if (windowEvent.WindowID == windowId)
                        {
                            _logger.LogInformation("Received WindowCloseRequested event");
                            exiting = true;
                        }
                        break;
                }
            }

            RenderFrame(gl);
        }
    }

    private unsafe void RenderFrame(GL gl)
    {
        gl.MakeCurrent();
        gl.ClearColor(0.1f, 0.1f, 0.1f, 1);
        gl.Clear(GLClearBufferMask.ColorBufferBit);

        // Render video frame
        var frame = _videoFrameManager!.AcquireCurrentFrame();
        if (frame != null)
        {
            _logger.LogTrace("Uploading frame to OpenGL");
            _glRenderer!.UploadFrame(frame);

            var frameData = frame.Frame;
            _uiRenderer!.UpdateFrameStatistics(
                frameData->width,
                frameData->height,
                frameData->format,
                frameData->pts,
                (frameData->flags & FFmpeg.AutoGen.ffmpeg.AV_FRAME_FLAG_KEY) != 0);

            _videoFrameManager.ReleaseFrame(frame);
            _logger.LogTrace("Frame returned to decoder");
        }

        if (_glRenderer != null && _glRenderer.HasFrame)
        {
            _glRenderer.Render();
        }

        // Render ImGui UI
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();

        var io = ImGui.GetIO();
        _uiRenderer!.RenderUi(io.DeltaTime);

        ImGui.Render();
        ImGui.EndFrame();

        gl.MakeCurrent();
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

        gl.MakeCurrent();
        gl.SwapBuffers();
    }

    private unsafe void CleanupResources(GL? gl, Hexa.NET.SDL3.SDLGLContext context, SDLWindow* window)
    {
        _logger.LogInformation("Cleaning up resources");

        _glRenderer?.Dispose();

        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplSDL3.Shutdown();
        ImGui.DestroyContext();
        gl?.Dispose();

        if (context != Hexa.NET.SDL3.SDLGLContext.Null)
        {
            SDL.GLDestroyContext(context);
        }

        if (window != null)
        {
            SDL.DestroyWindow(window);
        }

        SDL.Quit();
    }
}