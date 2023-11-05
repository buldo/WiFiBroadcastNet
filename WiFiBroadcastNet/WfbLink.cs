using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet;

public class WfbLink
{
    private readonly ILogger<WfbLink> _logger;
    private readonly List<DeviceHandler> _deviceHandlers;
    private readonly Dictionary<int, RadioStream> _radioStreams = new()
    {
        {128, new RadioStream(128, new NullFec(), new NullCrypto()) }
    };

    public WfbLink(
        IDevicesProvider devicesProvider,
        ILogger<WfbLink> logger)
    {
        _logger = logger;
        _deviceHandlers = devicesProvider
            .GetDevices()
            .Select(device => new DeviceHandler(device, ProcessRxFrame))
            .ToList();
    }

    public void Start()
    {
        foreach (var handler in _deviceHandlers)
        {
            handler.Start();
        }
    }

    private void ProcessRxFrame(RxFrame frame)
    {
        var filtered = FilterFrame(frame);
        if (filtered == null)
        {
            return;
        }

        // Process here
    }

    private  RxFrame? FilterFrame(RxFrame frame)
    {
        if (frame.Data.Length <= 0)
        {
            return null;
        }

        if (!frame.IsDataFrame())
        {
            return null;
        }

        //var arr = frame.Data.Select(c => $"{c:X2}").ToList();
        //var hexed = string.Join(' ', arr);
        //_logger.LogInformation(hexed);

        return frame;
    }

    public void SetChannel(WlanChannel wlanChannel)
    {
        foreach (var deviceHandler in _deviceHandlers)
        {
            deviceHandler.SetChannel(wlanChannel);
        }
    }
}

internal class DeviceHandler
{
    private readonly IRadioDevice _device;
    private readonly Action<RxFrame> _frameProcessAction;
    private readonly Channel<RxFrame> _framesChannel = Channel.CreateUnbounded<RxFrame>();
    private Task? _readTask;

    public DeviceHandler(
        IRadioDevice device,
        Action<RxFrame> frameProcessAction)
    {
        _device = device;
        _frameProcessAction = frameProcessAction;
        _device.AttachDataConsumer(_framesChannel.Writer);
    }

    public void Start()
    {
        _device.StartReceiving();
        _readTask = Task.Run(FrameReaderAsync);
    }

    private async Task FrameReaderAsync()
    {
        await foreach (var frame in _framesChannel.Reader.ReadAllAsync())
        {
            _frameProcessAction(frame);
        }
    }

    public void SetChannel(WlanChannel wlanChannel)
    {
        _device.SetChannel(wlanChannel);
    }
}