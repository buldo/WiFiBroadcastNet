using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.OpenGL;
using OpenHd.Ui.Configuration;
using SharpVideo.Decoding.Ffmpeg;
using SDLWindow = Hexa.NET.SDL3.SDLWindow;
using SDLEvent = Hexa.NET.SDL3.SDLEvent;
using SDL = Hexa.NET.SDL3.SDL;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// When we run application as desktop application, we use SDL3 with OpenGL3
/// </summary>
internal sealed class WindowedHost : UiHostBase<FfmpegH264Decoder, FfmpegDecodedFrame>
{
    private readonly WindowedHostConfiguration _configuration;

    private FfmpegGlRenderer? _glRenderer;

    protected override bool ShowDemoWindow => _configuration.ShowDemoWindow;

    public WindowedHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        FfmpegH264Decoder decoder,
        ILoggerFactory loggerFactory,
        ILogger<WindowedHost> logger,
        WindowedHostConfiguration? configuration = null)
        : base(h264Stream, decoder, loggerFactory, logger)
    {
        _configuration = configuration ?? new WindowedHostConfiguration();
        Logger.LogInformation("WindowedHost initialized");
    }

    protected override void RunDrawThread()
    {
        GL? gl = null;
        Hexa.NET.SDL3.SDLGLContext context = Hexa.NET.SDL3.SDLGLContext.Null;
        unsafe
        {
            SDLWindow* window = null;
            try
            {
                Logger.LogInformation("DrawThread started");

                // Initialize SDL and create window in the same thread that will handle events
                SDL.SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
                SDL.Init(Hexa.NET.SDL3.SDLInitFlags.Events | Hexa.NET.SDL3.SDLInitFlags.Video);

                float mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay());
                Logger.LogInformation("Display scale: {Scale}", mainScale);

                window = SDL.CreateWindow(
                    _configuration.WindowTitle,
                    (int)(_configuration.WindowWidth * mainScale),
                    (int)(_configuration.WindowHeight * mainScale),
                    Hexa.NET.SDL3.SDLWindowFlags.Resizable | Hexa.NET.SDL3.SDLWindowFlags.Opengl | Hexa.NET.SDL3.SDLWindowFlags.HighPixelDensity);
                var windowId = SDL.GetWindowID(window);
                Logger.LogInformation("Window created. Window ID: {WindowId}", windowId);

                Logger.LogInformation("Creating GL context");
                context = SDL.GLCreateContext(window);

                if (context.Handle == 0)
                {
                    Logger.LogError("Failed to create GL context");
                    return;
                }

                Logger.LogInformation("Creating ImGui context");
                var guiContext = Hexa.NET.ImGui.ImGui.CreateContext();
                Hexa.NET.ImGui.ImGui.SetCurrentContext(guiContext);

                // Setup ImGui config.
                var io = Hexa.NET.ImGui.ImGui.GetIO();
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

                var style = Hexa.NET.ImGui.ImGui.GetStyle();
                style.ScaleAllSizes(mainScale);
                style.FontScaleDpi = mainScale;
                io.ConfigDpiScaleFonts = true;
                io.ConfigDpiScaleViewports = true;

                Logger.LogInformation("Initializing ImGui SDL3 backend");
                ImGuiImplSDL3.SetCurrentContext(guiContext);
                if (!ImGuiImplSDL3.InitForOpenGL(new Hexa.NET.ImGui.Backends.SDL3.SDLWindowPtr((Hexa.NET.ImGui.Backends.SDL3.SDLWindow*)window), (void*)context.Handle))
                {
                    Logger.LogError("Failed to init ImGui Impl SDL3");
                    return;
                }

                Logger.LogInformation("Initializing ImGui OpenGL3 backend");
                ImGuiImplOpenGL3.SetCurrentContext(guiContext);
                if (!ImGuiImplOpenGL3.Init((byte*)null))
                {
                    Logger.LogError("Failed to init ImGui Impl OpenGL3");
                    return;
                }

                Logger.LogInformation("Creating GL bindings");
                gl = new GL(new BindingsContext(window, context));

                Logger.LogInformation("Creating video renderer");
                _glRenderer = new FfmpegGlRenderer(gl, LoggerFactory.CreateLogger<FfmpegGlRenderer>());

                Logger.LogInformation("Entering render loop");
                RunRenderLoop(window, windowId, gl);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception in DrawThread");
            }
            finally
            {
                CleanupResources(gl, context, window);
                Logger.LogInformation("DrawThread finished");
            }
        }
    }

    private unsafe void RunRenderLoop(SDLWindow* window, uint windowId, GL gl)
    {
        SDLEvent sdlEvent = default;
        bool exiting = false;

        while (!exiting && !CancellationTokenSource.Token.IsCancellationRequested)
        {
            SDL.PumpEvents();

            while (SDL.PollEvent(ref sdlEvent))
            {
                ImGuiImplSDL3.ProcessEvent((Hexa.NET.ImGui.Backends.SDL3.SDLEvent*)&sdlEvent);

                switch ((Hexa.NET.SDL3.SDLEventType)sdlEvent.Type)
                {
                    case Hexa.NET.SDL3.SDLEventType.Quit:
                        Logger.LogInformation("Received Quit event");
                        exiting = true;
                        break;

                    case Hexa.NET.SDL3.SDLEventType.Terminating:
                        Logger.LogInformation("Received Terminating event");
                        exiting = true;
                        break;

                    case Hexa.NET.SDL3.SDLEventType.WindowCloseRequested:
                        var windowEvent = sdlEvent.Window;
                        if (windowEvent.WindowID == windowId)
                        {
                            Logger.LogInformation("Received WindowCloseRequested event");
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
        var frame = VideoFrameManager!.AcquireCurrentFrame();
        if (frame is FfmpegDecodedFrame ffmpegFrame)
        {
            Logger.LogTrace("Uploading frame to OpenGL");
            _glRenderer!.UploadFrame(ffmpegFrame);

            var frameData = ffmpegFrame.Frame;
            UiRenderer!.UpdateFrameStatistics(
                frameData->width,
                frameData->height,
                frameData->format,
                frameData->pts,
                (frameData->flags & FFmpeg.AutoGen.ffmpeg.AV_FRAME_FLAG_KEY) != 0);

            VideoFrameManager.ReleaseFrame(frame);
            Logger.LogTrace("Frame returned to decoder");
        }
        else if (frame != null)
        {
            // Non-FFmpeg frame type - release without rendering
            Logger.LogWarning("WindowedHost only supports FFmpeg frames, got {Type}", frame.GetType().Name);
            VideoFrameManager.ReleaseFrame(frame);
        }

        if (_glRenderer != null && _glRenderer.HasFrame)
        {
            _glRenderer.Render();
        }

        // Render ImGui UI
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplSDL3.NewFrame();
        Hexa.NET.ImGui.ImGui.NewFrame();

        var io = Hexa.NET.ImGui.ImGui.GetIO();
        UiRenderer!.RenderUi(io.DeltaTime);

        Hexa.NET.ImGui.ImGui.Render();
        Hexa.NET.ImGui.ImGui.EndFrame();

        gl.MakeCurrent();
        ImGuiImplOpenGL3.RenderDrawData(Hexa.NET.ImGui.ImGui.GetDrawData());

        gl.MakeCurrent();
        gl.SwapBuffers();
    }

    private unsafe void CleanupResources(GL? gl, Hexa.NET.SDL3.SDLGLContext context, SDLWindow* window)
    {
        Logger.LogInformation("Cleaning up resources");

        _glRenderer?.Dispose();

        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplSDL3.Shutdown();
        Hexa.NET.ImGui.ImGui.DestroyContext();
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