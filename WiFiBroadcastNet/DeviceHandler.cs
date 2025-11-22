using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet;

internal class DeviceHandler
{
    private readonly IRadioDevice _device;

    public DeviceHandler(
        IRadioDevice device,
        Action<RxFrame> frameProcessAction)
    {
        _device = device;
        _device.AttachDataConsumer(frameProcessAction);
    }

    public void Start()
    {
        _device.StartReceiving();
    }

    public void SetChannelFrequency(ChannelFrequency wlanChannel)
    {
        _device.SetChannelFrequency(wlanChannel);
    }
}