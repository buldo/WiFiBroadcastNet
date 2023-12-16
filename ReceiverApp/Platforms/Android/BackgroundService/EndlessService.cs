using System;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Xamarin.KotlinX.Coroutines;

namespace ReceiverApp.Platforms.Android.BackgroundService;

[Service(Enabled = true, Exported = false)]
public class EndlessService : Service
{
    private PowerManager.WakeLock? wakeLock = null;
    private bool isServiceStarted = false;


    public override IBinder? OnBind(Intent? intent)
    {
        // log("Some component want to bind with the service")
        // We don't provide binding, so return null
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        //log("onStartCommand executed with startId: $startId")
        if (intent != null)
        {
            var action = intent.Action;
            //log("using an intent with action $action")

            if (action == Actions.START.ToString())
            {
                startService();
            }
            else if(action == Actions.STOP.ToString())
            {
                stopService();
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
        return StartCommandResult.Sticky;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        //log("The service has been created".toUpperCase());
        var notification = createNotification();
        StartForeground(1, notification);
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

    private void startService()
    {
        if (isServiceStarted)
        {
            return;
        }

        //log("Starting the foreground service task")
        Toast.MakeText(this, "Service starting its task", ToastLength.Short).Show();
        isServiceStarted = true;
        this.setServiceState(ServiceState.STARTED);

        // we need this lock so our service gets not affected by Doze Mode
        var pm = GetSystemService(Context.PowerService) as PowerManager;
        wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "EndlessService::lock");
        wakeLock.Acquire();

        // TODO: START HERE
        // we're starting a loop in a coroutine
        //GlobalScope.launch(Dispatchers.IO) {
        //    while (isServiceStarted)
        //    {
        //        launch(Dispatchers.IO) {
        //            pingFakeServer();
        //        }
        //        delay(1 * 60 * 1000);
        //    }
        //    log("End of the loop for the service")
        //}
    }

    private void stopService()
    {
        //log("Stopping the foreground service")
        Toast.MakeText(this, "Service stopping", ToastLength.Short)?.Show();
        try
        {
            if (wakeLock is { IsHeld: true })
            {
                wakeLock.Release();
            }

            StopForeground(true);
            StopSelf();
        }
        catch (Exception e) {
            //log("Service stopped without being started: ${e.message}")
        }
        isServiceStarted = false;
        this.setServiceState(ServiceState.STOPPED);
    }

    private void pingFakeServer()
    {
        //var df = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.mmmZ");
        //var gmtTime = df.format(Date());

        //var deviceId = Settings.Secure.getString(applicationContext.contentResolver, Settings.Secure.ANDROID_ID);

        var json =
            """
                {
                    "deviceId": "$deviceId",
                    "createdAt": "$gmtTime"
                }
            """;
        try
        {
            //Fuel.post("https://jsonplaceholder.typicode.com/posts")
            //    .jsonBody(json)
            //    .response { _, _, result ->
            //        val (bytes, error) = result
            //        if (bytes != null) {
            //            //log("[response bytes] ${String(bytes)}")
            //        } else {
            //            //log("[response error] ${error?.message}")
            //        }
            //    }
        }
        catch (Exception e)
        {
            //log("Error making the request: ${e.message}")
        }
    }

    private Notification createNotification()
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
            .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Drawable.ic_clear_black_24)
            .SetTicker("Ticker text")
            .Build();
        return notification;
    }
}