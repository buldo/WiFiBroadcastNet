using System.Threading.Channels;
using Bld.WlanUtils;
using WiFiBroadcastNet.Devices;

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

    public void SetChannel(WlanChannel wlanChannel)
    {
        _device.SetChannel(wlanChannel);
    }
}