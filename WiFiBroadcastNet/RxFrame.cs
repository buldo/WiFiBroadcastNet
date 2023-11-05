using System;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace WiFiBroadcastNet;

public class RxFrame
{
    private static readonly byte[] _dataHeader = { 0x08, 0x01 }; // Frame control value for QoS Data

    private readonly byte[] _data;

    public required byte[] Data
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

    public Span<byte> Payload => _data.AsSpan(24..^4);

    public UInt64 GetNonce()
    {
        Span<byte> data = stackalloc byte[8];

        MacSrcNoncePart1.CopyTo(data.Slice(0, 4));
        MacDstNoncePart2.CopyTo(data.Slice(4, 4));

        return BinaryPrimitives.ReadUInt64BigEndian(data);
    }

    public RadioPort get_valid_radio_port()
    {
        return RadioPort.FromByte(MacSrcRadioPort[0]);
    }

    public bool IsValidWfbFrame()
    {
        if (Data.Length <= 0)
        {
            return false;
        }

        if (!IsDataFrame())
        {
            return false;
        }

        if (Payload.Length == 0)
        {
            return false;
        }

        if (!HasValidAirGndId())
        {
            return false;
        }

        if (!HasValidRadioPort())
        {
            return false;
        }

        // TODO: add `frame.Payload.Length > RAW_WIFI_FRAME_MAX_PAYLOAD_SIZE`

        return true;
    }

    public byte GetValidAirGndId()
    {
        return MacSrcUniqueIdPart[0];
    }

    /// <summary>
    /// WiFi "Frame Control" value is "QoS Data"
    /// </summary>
    private bool IsDataFrame()
    {
        return ControlField[0] == _dataHeader[0] && ControlField[1] == _dataHeader[1];
    }

    /// <summary>
    /// Check - first byte of scr and dst mac needs to mach (unique air / gnd id)
    /// </summary>
    private bool HasValidAirGndId()
    {
        return MacSrcUniqueIdPart[0] == MacDstUniqueIdPart[0];
    }

    /// <summary>
    /// Check - last byte of src and dst mac needs to match (radio port)
    /// </summary>
    private bool HasValidRadioPort()
    {
        return MacSrcRadioPort[0] == MacDstRadioPort[0];
    }
}