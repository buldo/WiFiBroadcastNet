using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.App;
using Android.Content.PM;
using Android.Content;
using ReceiverApp.Platforms.Android.BackgroundService;

namespace ReceiverApp.Platforms.Android;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public MainActivity()
    {
        AndroidServiceManager.MainActivity = this;
    }

    public void StartService()
    {
        //var serviceIntent = new Intent(this, typeof(WfbBackgroundService));
        //serviceIntent.PutExtra("inputExtra", "Background Service");
        //StartForegroundService(serviceIntent);
        actionOnService(Actions.START);
    }

    public void StopService()
    {
        //var serviceIntent = new Intent(this, typeof(WfbBackgroundService));
        //StopService(serviceIntent);
        actionOnService(Actions.STOP);
    }

    private void actionOnService(Actions action)
    {
        if (this.getServiceState() == ServiceState.STOPPED && action == Actions.STOP)
        {
            return;
        }

        var serviceIntent = new Intent(this, typeof(EndlessService));
        serviceIntent.SetAction(action.ToString());
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            //log("Starting the service in >=26 Mode");
            StartForegroundService(serviceIntent);
            return;
        }

        //log("Starting the service in < 26 Mode");
        StartService(serviceIntent);
    }

}