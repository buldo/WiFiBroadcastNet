using Microsoft.Extensions.Logging;

using Rtl8812auNet;

using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet.GroundHost;

internal class Program
{
    static void Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole());

        using var driver = new WiFiDriver(loggerFactory);
        var devicesProvider = new AutoDevicesProvider(driver);
        var iface = new WfbLink(devicesProvider);
        iface.Start();
    }
}
