#nullable enable
namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;

internal class RtpPacket
{
    private readonly RtpHeader _header = new();
    private byte[]? _dataBuffer;
    private ReadOnlyMemory<byte> _rawPacket;
    private ReadOnlyMemory<byte> _payload;
    private bool _isReadyToUse;

    public RtpHeader Header
    {
        get
        {
            if (!_isReadyToUse)
            {
                throw new Exception("RtpPacket has no data");
            }

            return _header;
        }
    }

    public ReadOnlySpan<byte> Payload
    {
        get
        {
            if (!_isReadyToUse)
            {
                throw new Exception("RtpPacket has no data");
            }

            return _payload.Span;
        }
    }

    public void ApplyBuffer(byte[] data, int start, int length)
    {
        if (_isReadyToUse)
        {
            throw new Exception("RtpPacket already handle data");
        }

        _dataBuffer = data;
        _rawPacket = _dataBuffer.AsMemory(start, length);

        _header.ApplyData(_rawPacket.Span);
        _payload = _rawPacket[_header.Length..];
        _isReadyToUse = true;
    }

    public void ApplyBuffer(ReadOnlyMemory<byte> data)
    {
        if (_isReadyToUse)
        {
            throw new Exception("RtpPacket already handle data");
        }

        _rawPacket = data;

        _header.ApplyData(_rawPacket.Span);
        _payload = _rawPacket[_header.Length..];
        _isReadyToUse = true;
    }

    public byte[] ReleaseBuffer()
    {
        if (!_isReadyToUse)
        {
            throw new Exception("RtpPacket has no data");
        }

        _isReadyToUse = false;
        var temp = _dataBuffer!;
        _dataBuffer = null;
        _rawPacket = null;
        _payload = null;
        return temp;
    }
}