using System.Net;
using Bld.WlanUtils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WiFiBroadcastNet;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.Radio.Common;

namespace OpenHd.Ui;

public class WfbHost : IHostedService
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly WfbLink _iface;

    public WfbHost(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        var devicesProvider = new AutoDevicesProvider(loggerFactory);
        _iface = new WfbLink(
            devicesProvider,
            CreateAccessors(),
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

    private List<UserStream> CreateAccessors()
    {
        return new List<UserStream>
        {
            new()
            {
                StreamId = OpenHdRadioPorts.VIDEO_PRIMARY_RADIO_PORT,
                IsFecEnabled = true,
                StreamAccessor = new DummyStreamAccessor(_loggerFactory.CreateLogger<DummyStreamAccessor>()),
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
