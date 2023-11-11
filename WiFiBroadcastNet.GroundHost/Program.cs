using System.Net;
using Bld.WlanUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Rtl8812auNet;

using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet.GroundHost;

internal class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter("Rtl8812auNet.*", LogLevel.Warning)
            .AddConsole();
        builder.Services.AddHostedService<WfbHost>();

        var host = builder.Build();
        host.Run();
    }
}

public class WfbHost : IHostedService
{
    private readonly WiFiDriver _driver;
    private readonly WfbLink _iface;

    public WfbHost(ILoggerFactory loggerFactory)
    {
        _driver = new WiFiDriver(loggerFactory);
        var devicesProvider = new AutoDevicesProvider(_driver);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(loggerFactory),
            loggerFactory.CreateLogger<WfbLink>());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _iface.Start();
        _iface.SetChannel(Channels.Ch149);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //throw new NotImplementedException();
        return Task.CompletedTask;
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
                    new IPEndPoint(IPAddress.Parse("192.168.88.183"), 5600)),
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