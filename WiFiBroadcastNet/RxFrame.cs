using System;
using System.Buffers.Binary;

namespace WiFiBroadcastNet;

public class RxFrame
{
    private static readonly byte[] _dataHeader = { 0x08, 0x01 };

    private readonly byte[] _data;

    public byte[] Data
    {
        get => _data;
        init
        {
            _data = value;
            DataAsMemory = _data;
        }
    }

    public Memory<byte> DataAsMemory { get; private set; }

    public Span<byte> ControlField => _data.AsSpan(0, 2);

    public Span<byte> Duration => _data.AsSpan(2, 2);

    public Span<byte> MacAp => _data.AsSpan(4, 6);

    public Span<byte> MacSrcUniqueIdPart => _data.AsSpan(10, 1);

    public Span<byte> MacSrcNoncePart1 => _data.AsSpan(11, 4);

    public Span<byte> MacSrcRadioPort => _data.AsSpan(15, 1);

    public Span<byte> MacDstUniqueIdPart => _data.AsSpan(16, 1);

    public Span<byte> MacDstNoncePart2 => _data.AsSpan(17, 4);

    public Span<byte> MacDstRadioPort => _data.AsSpan(21, 1);

    public Span<byte> SequenceControl => _data.AsSpan(22, 2);

    public UInt64 GetNonce()
    {
        Span<byte> data = stackalloc byte[8];

        MacSrcNoncePart1.CopyTo(data.Slice(0, 4));
        MacDstNoncePart2.CopyTo(data.Slice(4, 4));

        return BinaryPrimitives.ReadUInt64BigEndian(data);
    }

    public bool IsDataFrame()
    {
        return ControlField[0] == _dataHeader[0] && ControlField[1] == _dataHeader[1];
    }
}