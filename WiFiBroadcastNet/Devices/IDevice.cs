using System.Threading.Channels;

using Bld.WlanUtils;

namespace WiFiBroadcastNet.Devices;

public interface IDevice
{
    void StartReceiving(ChannelWriter<RxFrame> receivedFramesChannel);

    void SetChannel(WlanChannel channel);
}