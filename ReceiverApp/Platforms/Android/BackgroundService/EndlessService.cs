using System.Net;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using Bld.WlanUtils;
using Microsoft.Extensions.Logging;

using Rtl8812auNet;
using WiFiBroadcastNet;

namespace ReceiverApp.Platforms.Android.BackgroundService;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]

public class EndlessService : Service
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EndlessService> _logger;

    private PowerManager.WakeLock? _wakeLock = null;
    private bool _isServiceStarted = false;

    private WiFiDriver? _driver;
    private WfbLink? _iface;
    private Task _startTask;

    public EndlessService()
    {
        _loggerFactory = IPlatformApplication.Current.Services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<EndlessService>();
    }

    public WlanChannel SelectedChannel { get; private set; } = Channels.Ch149;

    public override IBinder? OnBind(Intent? intent)
    {
        // log("Some component want to bind with the service")
        // We don't provide binding, so return null
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        AndroidServiceManager.RegisterServiceInstance(this);
        //log("onStartCommand executed with startId: $startId")
        if (intent != null)
        {
            var action = intent.Action;
            //log("using an intent with action $action")

            if (action == Actions.START.ToString())
            {
                StartService();
            }
            else if(action == Actions.STOP.ToString())
            {
                StopService();
            }
            else
            {
                //log("This should never happen. No action in the received intent");
            }
        }
        else
        {
            //log("with a null intent. It has been probably restarted by the system.");
        }
        // by returning this we make sure the service is restarted if the system kills the service
        return StartCommandResult.RedeliverIntent;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        //log("The service has been created".toUpperCase());
        var notification = CreateNotification();
        StartForeground(1, notification, ForegroundService.TypeDataSync);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        //log("The service has been destroyed".toUpperCase())
        Toast.MakeText(this, "Service destroyed", ToastLength.Short).Show();
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        var restartServiceIntent = new Intent(base.ApplicationContext, typeof(EndlessService));
        restartServiceIntent.SetPackage(base.PackageName);

        var restartServicePendingIntent = PendingIntent.GetService(this, 1, restartServiceIntent, PendingIntentFlags.OneShot);
        ApplicationContext.GetSystemService(Context.AlarmService);
        var alarmService = ApplicationContext.GetSystemService(Context.AlarmService) as AlarmManager;
        alarmService.Set(AlarmType.ElapsedRealtime, SystemClock.ElapsedRealtime() + 1000, restartServicePendingIntent);
    }

    private void StartService()
    {
        if (_isServiceStarted)
        {
            return;
        }

        //log("Starting the foreground service task")
        Toast.MakeText(this, "Service starting its task", ToastLength.Short).Show();
        _isServiceStarted = true;
        this.setServiceState(ServiceState.STARTED);

        // we need this lock so our service gets not affected by Doze Mode
        var pm = GetSystemService(Context.PowerService) as PowerManager;
        _wakeLock = pm.NewWakeLock(WakeLockFlags.Full, "EndlessService::lock");
        _wakeLock.Acquire();
    }

    private void StopService()
    {
        //log("Stopping the foreground service")
        Toast.MakeText(this, "Service stopping", ToastLength.Short)?.Show();
        try
        {
            if (_wakeLock is { IsHeld: true })
            {
                _wakeLock.Release();
            }

            StopForeground(true);
            StopSelf();
        }
        catch (Exception e) {
            //log("Service stopped without being started: ${e.message}")
        }
        _isServiceStarted = false;
        this.setServiceState(ServiceState.STOPPED);
    }

    private Notification CreateNotification()
    {
        var notificationChannelId = "ENDLESS SERVICE CHANNEL";

        var notificationManager = GetSystemService(Context.NotificationService) as NotificationManager;

        // depending on the Android API that we're dealing with we will have
        // to use a specific method to create the notification
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                notificationChannelId,
                "Endless Service notifications channel",
                NotificationImportance.High
            );

            channel.Description = "Endless Service channel";
            channel.EnableLights(true);
            //channel.LightColor = Colors.Red;
            channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 100, 200, 300, 400, 500, 400, 300, 200, 400 });

            notificationManager.CreateNotificationChannel(channel);
        }

        var pendingIntent = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)),
            PendingIntentFlags.Immutable);

        var builder = new Notification.Builder(this, notificationChannelId);
        var notification = builder
            .SetContentTitle("Endless Service")
            .SetContentText("This is your favorite endless service working")
            .SetContentIntent(pendingIntent)
            .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Drawable.dotnet_bot)
            .SetTicker("Ticker text")
            .SetOngoing(true)
            .Build();
        return notification;
    }

    public void StartRx(UsbDevice device, UsbDeviceConnection connection)
    {
        _startTask = Task.Run(() => StartRxInternal(device, connection));
    }

    private void StartRxInternal(UsbDevice device, UsbDeviceConnection connection)
    {
        try
        {
            _driver = new WiFiDriver(_loggerFactory);
            var devicesProvider = new AndroidDevicesProvider(
                _driver,
                device,
                connection,
                _loggerFactory);
            _iface = new WfbLink(
                devicesProvider,
                CreateAccessors(_loggerFactory),
                _loggerFactory.CreateLogger<WfbLink>());
            _iface.Start();
            _iface.SetChannel(SelectedChannel);



            NotifyStartResult(true, null);
        }
        catch (Exception e)
        {
            NotifyStartResult(false, e.Message);
        }
    }

    private static List<UserStream> CreateAccessors(ILoggerFactory factory)
    {
        return
        [
            new()
            {
                StreamId = RadioPorts.VIDEO_PRIMARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new UdpTransferAccessor(
                    factory.CreateLogger<UdpTransferAccessor>(),
                    new IPEndPoint(IPAddress.Loopback, 5600)),
            },
            new()
            {
                StreamId = RadioPorts.VIDEO_SECONDARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
            new()
            {
                StreamId = RadioPorts.TELEMETRY_WIFIBROADCAST_TX_RADIO_PORT,
                IsFecEnabled = false,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
            new()
            {
                StreamId = RadioPorts.MANAGEMENT_RADIO_PORT_AIR_TX,
                IsFecEnabled = false,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
        ];
    }


    private void NotifyStartResult(
        bool isSuccess,
        string? errorText)
    {
        var intent = new Intent(IntentActions.ServiceRxStarted);
        var result = new RxStartResult(isSuccess, errorText);
        var data = JsonSerializer.Serialize(result);
        intent.PutExtra(nameof(RxStartResult), data);
        SendBroadcast(intent);
    }
}