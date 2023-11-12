using System.Net;
using Android.App;
using Android.Content;
using Android.Mtp;
using Android.OS;
using Bld.WlanUtils;
using LibUsbDotNet.LibUsb;
using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using Rtl8812auNet.Rtl8812au;
using WiFiBroadcastNet;
using WiFiBroadcastNet.Devices;

namespace ReceiverApp.Platforms.Android;

[Service]
public class WfbBackgroundService : Service
{
    private WiFiDriver? _driver;
    private WfbLink? _iface;

    private Task _task;
    //    Timer timer = null;
    //    int myId = (new object()).GetHashCode();
    //    int BadgeNumber = 0;

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Start();
        return StartCommandResult.Sticky;
    }

    private void Start()
    {
        var loggerFactory = IPlatformApplication.Current.Services.GetRequiredService<ILoggerFactory>();
        _driver = new WiFiDriver(loggerFactory, false);
        var devicesProvider = new AndroidDevicesProvider(_driver, loggerFactory);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(loggerFactory),
            loggerFactory.CreateLogger<WfbLink>());
        _iface.Start();
        _iface.SetChannel(Channels.Ch149);
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
            new()
            {
                StreamId = RadioPorts.VIDEO_SECONDARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
            new()
            {
                StreamId = RadioPorts.TELEMETRY_WIFIBROADCAST_TX_RADIO_PORT,
                IsFecEnabled = false,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
            new()
            {
                StreamId = RadioPorts.MANAGEMENT_RADIO_PORT_AIR_TX,
                IsFecEnabled = false,
                StreamAccessor = new UdpTransferAccessor(factory.CreateLogger<UdpTransferAccessor>(), null),
            },
        };
    }
}

public class AndroidDevicesProvider : IDevicesProvider
{
    private readonly WiFiDriver _wiFiDriver;
    private readonly ILoggerFactory _loggerFactory;

    public AndroidDevicesProvider(
        WiFiDriver wiFiDriver,
        ILoggerFactory loggerFactory)
    {
        _wiFiDriver = wiFiDriver;
        _loggerFactory = loggerFactory;
    }

    public List<IRadioDevice> GetDevices()
    {
        var device = AndroidServiceManager.Device;
        var connection = AndroidServiceManager.Connection;


        List<IRadioDevice> devices = new();

        var adapter = new RtlUsbDevice(device, connection, _loggerFactory.CreateLogger<RtlUsbDevice>());
        var d1 = _wiFiDriver.CreateRtlDevice(adapter);
        var wrapper = new UserspaceRadioDevice(d1);
        devices.Add(wrapper);

        return devices;
    }
}