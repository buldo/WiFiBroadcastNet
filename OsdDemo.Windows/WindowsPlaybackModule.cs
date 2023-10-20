using FlyleafLib;

namespace OsdDemo.Windows;

public class WindowsPlaybackModule
{
    public WindowsPlaybackModule()
    {
        Engine.Start(new EngineConfig()
        {
            FFmpegPath = @"FFmpeg",
            FFmpegDevices = false,    // Prevents loading avdevice/avfilter dll files. Enable it only if you plan to use dshow/gdigrab etc.

#if RELEASE
            FFmpegLogLevel      = FFmpegLogLevel.Quiet,
            LogLevel            = LogLevel.Quiet,

#else
            FFmpegLogLevel = FFmpegLogLevel.Warning,
            LogLevel = LogLevel.Debug,
            LogOutput = ":debug",
            //LogOutput         = ":console",
            //LogOutput         = @"C:\Flyleaf\Logs\flyleaf.log",
#endif

            //PluginsPath       = @"C:\Flyleaf\Plugins",

            UIRefresh = false,    // Required for Activity, BufferedDuration, Stats in combination with Config.Player.Stats = true
            UIRefreshInterval = 250,      // How often (in ms) to notify the UI
            UICurTimePerSecond = true,     // Whether to notify UI for CurTime only when it's second changed or by UIRefreshInterval
        });

        ViewModel = new VideoWindowViewModel();
        VideoWindow = new VideoWindow()
        {
            DataContext = ViewModel
        };
    }

    public VideoWindow VideoWindow { get; }

    public VideoWindowViewModel ViewModel { get; }
}