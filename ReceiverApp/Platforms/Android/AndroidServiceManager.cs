using Android.Hardware.Usb;
using ReceiverApp.Platforms.Android.BackgroundService;

namespace ReceiverApp.Platforms.Android;

public static class AndroidServiceManager
{
    private static EndlessService? _endlessService;

    public static MainActivity? MainActivity { get; set; }

    public static bool IsRunning { get; set; }

    public static EndlessService? Service => _endlessService;

    public static void RegisterServiceInstance(EndlessService service)
    {
        _endlessService = service;
    }

    public static void StartWfbService()
    {
        if (MainActivity == null)
        {
            return;
        }

        MainActivity.StartService();
    }

    public static void StopWfbService()
    {
        if (MainActivity == null)
        {
            return;
        }
        MainActivity.StopService();
        IsRunning = false;
    }
}