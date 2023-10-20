using System.Threading.Channels;

using Bld.WlanUtils;

namespace WiFiBroadcastNet.Devices;

public interface IRadioDevice
{
    void AttachDataConsumer(ChannelWriter<RxFrame> receivedFramesChannel);

    void StartReceiving();

    void SetChannel(WlanChannel channel);
}