using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.OpenGL;
using Hexa.NET.SDL3;

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

    private Task? _drawThread;

    public WindowedHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        ILogger<WindowedHost> logger)
        : base(h264Stream, logger)
    {
        _logger = logger;
        _logger.LogInformation("WindowedHost initialized");
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting WindowedHost draw thread");
        _drawThread = Task.Factory.StartNew(DrawThread, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WindowedHost");
        await _cancellationTokenSource.CancelAsync();

        if (_drawThread != null)
        {
            await _drawThread;
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
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
            io.ConfigViewportsNoAutoMerge = false;
            io.ConfigViewportsNoTaskBarIcon = false;

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
                GL.ClearColor(1, 0.8f, 0.75f, 1);
                GL.Clear(GLClearBufferMask.ColorBufferBit);

                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplSDL3.NewFrame();
                ImGui.NewFrame();

                ImGui.ShowDemoWindow();

                ImGui.Render();
                ImGui.EndFrame();

                GL.MakeCurrent();
                ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

                if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
                {
                    ImGui.UpdatePlatformWindows();
                    ImGui.RenderPlatformWindowsDefault();
                }

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

            // Cleanup in the same thread
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