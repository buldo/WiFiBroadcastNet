using WiFiBroadcastNet;

namespace OpenHd.Ui;

public class InMemoryPipeStreamAccessor : IStreamAccessor
{
    private Action<ReadOnlyMemory<byte>>? _action;

    public void SetReceiveAction(Action<ReadOnlyMemory<byte>> action)
    {
        _action = action;
    }

    public void ProcessIncomingFrame(ReadOnlyMemory<byte> payload)
    {
        _action?.Invoke(payload);
    }
}