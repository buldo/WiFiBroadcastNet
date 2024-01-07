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
        //JavaSystem.LoadLibrary();
#endif
        //NativeLibrary.SetDllImportResolver(typeof(SpaceWizards.Sodium.Interop.Libsodium).Assembly, DllImportResolver);
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug().SetMinimumLevel(LogLevel.Information);
#endif

        return builder.Build();
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "libsodium")
        {
            Console.WriteLine("sss");
            //// On systems with AVX2 support, load a different library.
            //if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            //{
            //    return NativeLibrary.Load("nativedep_avx2", assembly, searchPath);
            //}
        }

        // Otherwise, fallback to default import resolver.
        return IntPtr.Zero;
    }
}

