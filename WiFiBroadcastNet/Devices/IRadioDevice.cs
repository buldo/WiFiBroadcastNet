using System.Threading.Channels;

using Bld.WlanUtils;

namespace WiFiBroadcastNet.Devices;

public interface IRadioDevice
{
    void AttachDataConsumer(Action<RxFrame> receivedFramesChannel);

    void StartReceiving();

    void SetChannel(WlanChannel channel);
}