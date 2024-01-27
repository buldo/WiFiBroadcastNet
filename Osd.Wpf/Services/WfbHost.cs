using System.Net;
using Bld.WlanUtils;
using CommonAbstractions;
using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using WiFiBroadcastNet;
using WiFiBroadcastNet.Devices;

namespace Osd.Wpf.Services;

public class WfbHost : IWfbHost
{
    private readonly WiFiDriver _wifiDriver;
    private readonly ILoggerFactory _loggerFactory;

    private WfbLink? _iface;

    public WfbHost(WiFiDriver wifiDriver, ILoggerFactory loggerFactory)
    {
        _wifiDriver = wifiDriver;
        _loggerFactory = loggerFactory;
    }

    public int GetDevicesCount()
    {
        return _wifiDriver.GetUsbDevices().Count;
    }

    public void Start(WlanChannel startChannel)
    {
        if (_iface == null)
        {
            return;
        }

        var devicesProvider = new AutoDevicesProvider(_wifiDriver);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(_loggerFactory),
            _loggerFactory.CreateLogger<WfbLink>());

        _iface.Start();
        _iface.SetChannel(startChannel);
    }

    public void SetChannel(WlanChannel channel)
    {
        if (_iface != null)
        {
            _iface.SetChannel(channel);
        }
    }

    public void Stop()
    {
        //throw new NotImplementedException();
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