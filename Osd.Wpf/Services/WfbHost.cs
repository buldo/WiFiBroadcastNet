using System.Net;
using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using WiFiBroadcastNet;
using WiFiBroadcastNet.Devices;

namespace Osd.Wpf.Services;

public class WfbHost
{
    private readonly WfbLink _iface;

    public WfbHost(WiFiDriver wifiDriver, ILoggerFactory loggerFactory)
    {
        var devicesProvider = new AutoDevicesProvider(wifiDriver);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(loggerFactory),
            loggerFactory.CreateLogger<WfbLink>());
    }

    public void Start(WlanChannel startChannel)
    {
        _iface.Start();
        _iface.SetChannel(startChannel);
    }

    public void SetChannel(WlanChannel channel)
    {
        _iface.SetChannel(channel);
    }

    private List<UserStream> CreateAccessors(ILoggerFactory factory)
    {
        return new List<UserStream>
        {
            new()
            {
                StreamId = RadioPorts.VIDEO_PRIMARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new UdpTransferAccessor(
                    factory.CreateLogger<UdpTransferAccessor>(),
                    new IPEndPoint(IPAddress.Loopback, 5600)),
            },
            //new()
            //{
            //    StreamId = RadioPorts.VIDEO_SECONDARY_RADIO_PORT,
            //    IsFecEnabled = true,
            //    StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            //},
            //new()
            //{
            //    StreamId = RadioPorts.TELEMETRY_WIFIBROADCAST_TX_RADIO_PORT,
            //    IsFecEnabled = false,
            //    StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            //},
            //new()
            //{
            //    StreamId = RadioPorts.MANAGEMENT_RADIO_PORT_AIR_TX,
            //    IsFecEnabled = false,
            //    StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            //},
        };
    }
}