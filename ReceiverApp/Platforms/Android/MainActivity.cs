using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.App;
using Android.Content.PM;
using Android.Content;

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
        var serviceIntent = new Intent(this, typeof(WfbBackgroundService));
        serviceIntent.PutExtra("inputExtra", "Background Service");
        StartForegroundService(serviceIntent);
    }

    public void StopService()
    {
        var serviceIntent = new Intent(this, typeof(WfbBackgroundService));
        StopService(serviceIntent);
    }
}