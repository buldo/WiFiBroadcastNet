using Android.Content;

namespace ReceiverApp.Platforms.Android.BackgroundService;

internal static class ContextExtensions
{
    private const string name = "SPYSERVICE_KEY";
    private const string key = "SPYSERVICE_STATE";

    public static ServiceState getServiceState(this Context context)
    {
        var sharedPrefs = getPreferences(context);
        var value = sharedPrefs.GetString(key, ServiceState.STOPPED.ToString());
        return Enum.Parse<ServiceState>(value);
    }

    public static void setServiceState(this Context context, ServiceState state)
    {
        var sharedPrefs = getPreferences(context);
        using var editor = sharedPrefs.Edit();
        editor.PutString(key, state.ToString());
        editor.Apply();
    }

    private static ISharedPreferences getPreferences(Context context)
    {
        return context.GetSharedPreferences(name, 0);
    }
}