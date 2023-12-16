using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.OS;

using AndroidX.Core.Content;

namespace ReceiverApp.Platforms.Android.BackgroundService;

[BroadcastReceiver(Enabled = true, Permission = Manifest.Permission.ReceiveBootCompleted, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted }, Priority = (int)IntentFilterPriority.LowPriority,
    Categories = new[] { Intent.CategoryDefault })]
public class StartReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent.Action == Intent.ActionBootCompleted && context.getServiceState() == ServiceState.STARTED)
        {
            var it = new Intent(context, typeof(EndlessService));

            it.SetAction(Actions.START.ToString());
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                //log("Starting the service in >=26 Mode from a BroadcastReceiver")
                context.StartForegroundService(it);
                return;
            }

            //log("Starting the service in < 26 Mode from a BroadcastReceiver")
            context.StartService(it);
        }
    }
}