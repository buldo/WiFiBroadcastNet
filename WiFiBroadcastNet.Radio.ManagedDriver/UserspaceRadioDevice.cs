using Rtl8812auNet;
using Rtl8812auNet.Rtl8812au;
using Rtl8812auNet.Rtl8812au.Enumerations;
using Rtl8812auNet.Rtl8812au.Models;

using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.Radio.ManagedDriver;

public class UserspaceRadioDevice : IRadioDevice
{
    private readonly Rtl8812aDevice _rtlDevice;

    private Action<RxFrame> _receivedFramesChannelWriter;
    private byte _channel = 36;
    private bool _isStarted = false;

    public UserspaceRadioDevice(Rtl8812aDevice rtlDevice)
    {
        _rtlDevice = rtlDevice;
    }

    public void AttachDataConsumer(Action<RxFrame> receivedFramesChannel)
    {
        _receivedFramesChannelWriter = receivedFramesChannel;
    }

    public void StartReceiving()
    {
        if (_isStarted == true)
        {
            throw new Exception("Already started");
        }

        StartDevice();
        ApplyChannel();
        _isStarted = true;
    }

    public void SetChannelFrequency(ChannelFrequency channelFrequency)
    {
        _channel = (byte)channelFrequency.Channel;
        if (_isStarted)
        {
            ApplyChannel();
        }
    }

    private void StartDevice()
    {
        _rtlDevice.Init(PacketProcessor, CreateCurrentChannel());
        ApplyChannel();
    }

    private void PacketProcessor(ParsedRadioPacket arg)
    {
        _receivedFramesChannelWriter(new RxFrame
        {
            Data = arg.Data,
        });
    }

    private void ApplyChannel()
    {
        _rtlDevice.SetMonitorChannel(CreateCurrentChannel());
    }

    private SelectedChannel CreateCurrentChannel()
    {
        return new SelectedChannel
        {
            Channel = (byte)_channel,
            ChannelOffset = 0,
            ChannelWidth = ChannelWidth.CHANNEL_WIDTH_20
        };
    }
}
