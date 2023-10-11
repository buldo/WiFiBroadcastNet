using System.Threading.Channels;

using Bld.WlanUtils;

namespace WiFiBroadcastNet.Devices;

public interface IRadioDevice
{
    void StartReceiving(ChannelWriter<RxFrame> receivedFramesChannel);

    void SetChannel(WlanChannel channel);
}