using System.Net;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Mtp;
using Android.OS;
using Android.Widget;

using Bld.WlanUtils;
using LibUsbDotNet.LibUsb;
using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using Rtl8812auNet.Rtl8812au;
using WiFiBroadcastNet;
using AndroidApp = Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Views;

using LibUsbDotNet;

using Context = Android.Content.Context;

namespace ReceiverApp.Platforms.Android.BackgroundService;

public class WfbBackgroundService
{
    private PowerManager.WakeLock? wakeLock;
    private bool isServiceStarted = false;




    private Task _task;

    private readonly ILoggerFactory _loggerFactory;

    private readonly ILogger<WfbBackgroundService> _logger;
    //    Timer timer = null;
    //    int myId = (new object()).GetHashCode();
    //    int BadgeNumber = 0;

    public WfbBackgroundService()
    {
        _loggerFactory = IPlatformApplication.Current.Services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<WfbBackgroundService>();
    }

    private void Start()
    {


    }


    private void StartService()
    {
        if (isServiceStarted)
        {
            _logger.LogInformation("Starting the foreground service task");
            return;
        }

        //Toast.MakeText(this, "Service starting its task", ToastLength.Short).Show();
        isServiceStarted = true;
        //SetServiceState(this, ServiceState.STARTED);

        var context = AndroidApp.Application.Context;
        //var powerManager = (PowerManager)context.GetSystemService(PowerService);

        // we need this lock so our service gets not affected by Doze Mode
        //wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "WfbReceiver::lock");
        //wakeLock.Acquire();

        // we're starting a loop in a coroutine
        // TODO
        //GlobalScope.launch(Dispatchers.IO) {
        //    while (isServiceStarted)
        //    {
        //        launch(Dispatchers.IO)
        //        {
        //            pingFakeServer();
        //        }
        //        delay(1 * 60 * 1000);
        //    }

        //    log("End of the loop for the service");
        //}
    }
}