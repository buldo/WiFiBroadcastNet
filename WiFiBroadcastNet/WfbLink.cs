using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet;

public class WfbLink
{
    private readonly List<DeviceHandler> _deviceHandlers;
    private readonly Dictionary<int, RadioStream> _radioStreams = new()
    {
        {128, new RadioStream(128, new NullFec(), new NullCrypto()) }
    };

    public WfbLink(IDevicesProvider devicesProvider)
    {
        _deviceHandlers = devicesProvider
            .GetDevices()
            .Select(device => new DeviceHandler(device))
            .ToList();
    }

    public void Start()
    {
        foreach (var handler in _deviceHandlers)
        {
            handler.Start();
        }
    }
}

internal class DeviceHandler
{
    private readonly IRadioDevice _device;
    private readonly Channel<RxFrame> _framesChannel = Channel.CreateUnbounded<RxFrame>();

    public DeviceHandler(IRadioDevice device)
    {
        _device = device;
        _device.AttachDataConsumer(_framesChannel.Writer);
    }

    public void Start()
    {
        _device.StartReceiving();
    }
}