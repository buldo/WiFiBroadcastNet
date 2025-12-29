using System.Threading.Channels;
using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.Rx;

public class Receiver
{
    private readonly List<AttachedDevice> _devices = new();

    public Receiver(IEnumerable<IRadioDevice> devices)
    {
        foreach (var device in devices)
        {
            AttachDevice(device);
        }
    }

    public void Start()
    {

    }

    private void AttachDevice(IRadioDevice device)
    {
        var at = new AttachedDevice(device);
        _devices.Add(at);
    }

    private class AttachedDevice
    {
        public AttachedDevice(IRadioDevice device)
        {
            Device = device;

        }

        public IRadioDevice Device { get; }

        public Channel<RxFrame> FramesChannel { get; } = Channel.CreateUnbounded<RxFrame>(new()
            { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true });
    }
}