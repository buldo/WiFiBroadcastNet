using System.Threading.Channels;
using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet.Rx;

public class Receiver
{
    private readonly List<AttachedDevice> _devices = new();

    public Receiver(IEnumerable<IDevice> devices)
    {
        foreach (var device in devices)
        {
            AttachDevice(device);
        }
    }

    public void Start()
    {

    }

    private void AttachDevice(IDevice device)
    {
        var at = new AttachedDevice(device);
        _devices.Add(at);
    }

    private class AttachedDevice
    {
        public AttachedDevice(IDevice device)
        {
            Device = device;
            device.AttachReader(FramesChannel.Writer);
        }

        public IDevice Device { get; }

        public Channel<RxFrame> FramesChannel { get; } = Channel.CreateUnbounded<RxFrame>(new()
            { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true });
    }
}