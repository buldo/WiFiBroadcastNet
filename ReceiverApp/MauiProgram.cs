#if ANDROID
using System.Reflection;
using System.Runtime.InteropServices;

using Java.Lang;
#endif

using Microsoft.Extensions.Logging;

namespace ReceiverApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        JavaSystem.LoadLibrary("sodium");
#endif
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug().SetMinimumLevel(LogLevel.Warning);
#endif

        return builder.Build();
    }
}

