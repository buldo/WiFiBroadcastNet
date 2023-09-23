using System.Threading.Channels;

namespace WiFiBroadcastNet;

public interface IDevice
{
    public void AttachReader(ChannelWriter<RxFrame> receivedFramesChannel);
}