namespace WiFiBroadcastNet.Radio.Common;

public interface IRadioDevice
{
    void AttachDataConsumer(Action<RxFrame> receivedFramesChannel);

    void StartReceiving();

    void SetChannelFrequency(ChannelFrequency channelFrequency);
}