using Bld.Libnl;
using Bld.Libnl.Types;
using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using SharpPcap.LibPcap;
using WiFiBroadcastNet.Radio.Common;
#pragma warning disable CA1873

namespace WiFiBroadcastNet.Radio.LinuxPcap;

public class PcapDevicesProvider : IDevicesProvider
{
    private readonly ILogger<PcapDevicesProvider> _logger;
    private readonly WlanManager _wlanManager;

    public PcapDevicesProvider(
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PcapDevicesProvider>();
        _wlanManager = new WlanManager(loggerFactory.CreateLogger<WlanManager>());
    }

    public List<IRadioDevice> GetDevices()
    {
        var allMonitorDevices = _wlanManager
            .GetWlanInterfaces()
            .Where(d => d.SupportedInterfaceTypes?.Contains(Nl80211InterfaceType.NL80211_IFTYPE_MONITOR) ?? false);
        var selectedDevice =
            allMonitorDevices.First(d => d.DriverName.Contains("8812") || d.DriverName.Contains("rtw88"));
        _logger.LogInformation("Selected device: {Interface}; Driver: {Driver}; Monitor support: {Monitor}",
            selectedDevice.InterfaceName,
            selectedDevice.DriverName,
            selectedDevice.CurrentInterfaceMode
        );

        if (selectedDevice.CurrentInterfaceMode != Nl80211InterfaceType.NL80211_IFTYPE_MONITOR)
        {
            _wlanManager.TrySwitchToMonitorAsync(selectedDevice).GetAwaiter().GetResult();
        }

        //_wlanManager.SetChannel(selectedDevice, ChannelFrequencies.Width20MHz.Ch149Fr5745.Frequency, ChannelModes.ModeHt20);
        var pcapDevice = LibPcapLiveDeviceList.Instance[selectedDevice.InterfaceName];
        return [new PcapRadioDevice(selectedDevice, pcapDevice, _wlanManager)];
    }
}
