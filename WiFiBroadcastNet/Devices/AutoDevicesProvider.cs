using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Radio.Common;

#if WINDOWS
using Rtl8812auNet;
using WiFiBroadcastNet.Radio.ManagedDriver;
#else
using WiFiBroadcastNet.Radio.LinuxPcap;
#endif

namespace WiFiBroadcastNet.Devices;

public class AutoDevicesProvider : IDevicesProvider
{
    private readonly IDevicesProvider _devicesProvider;

    public AutoDevicesProvider(ILoggerFactory loggerFactory)
    {
#if WINDOWS
        _devicesProvider = new UserspaceDevicesProvider(new WiFiDriver(loggerFactory));
#else
        _devicesProvider = new PcapDevicesProvider(loggerFactory);
#endif
    }

    public List<IRadioDevice> GetDevices()
    {
        return _devicesProvider.GetDevices();
    }
}
