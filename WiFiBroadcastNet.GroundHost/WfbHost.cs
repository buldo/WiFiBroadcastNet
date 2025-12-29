using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.GroundHost;

public class WfbHost : IHostedService
{
    private readonly WfbLink _iface;

    public WfbHost(ILoggerFactory loggerFactory)
    {
        var devicesProvider = new AutoDevicesProvider(loggerFactory);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(loggerFactory),
            loggerFactory.CreateLogger<WfbLink>());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _iface.Start();
        _iface.SetChannel(ChannelFrequencies.Width20MHz.Ch149Fr5745);
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
                StreamId = OpenHdRadioPorts.VIDEO_PRIMARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new UdpTransferAccessor(
                    factory.CreateLogger<UdpTransferAccessor>(),
                    new IPEndPoint(IPAddress.Parse("192.168.88.183"), 5600)),
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