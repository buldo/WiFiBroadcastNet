using System.Threading.Channels;
using Bld.WlanUtils;
using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet;

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