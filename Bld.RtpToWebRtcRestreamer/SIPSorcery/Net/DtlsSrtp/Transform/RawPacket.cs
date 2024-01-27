using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class RawPacket
{

    public const int RTP_PACKET_MAX_SIZE = 8192;
    /**
         * The size of the extension header as defined by RFC 3550.
         */
    private const int EXT_HEADER_SIZE = 4;

    /**
         * The size of the fixed part of the RTP header as defined by RFC 3550.
         */
    private const int FIXED_HEADER_SIZE = 12;

    /**
         * Byte array storing the content of this Packet
         */
    private byte[] _buffer;

    private Memory<byte> _data;

    /// <summary>
    /// Invoked
    /// </summary>
    public RawPacket()
    {
        _buffer = new byte[RTP_PACKET_MAX_SIZE];
    }

    public void Wrap(byte[] buffer, int length)
    {
        _buffer = buffer.AsSpan().ToArray();
        _data = _buffer.AsMemory(0, length);
    }

    public void WrapNoCopy(byte[] buffer, int length)
    {
        _buffer = buffer;
        _data = _buffer.AsMemory(0, length);
    }

    public ReadOnlyMemory<byte> GetMemory()
    {
        return _data;
    }

    public byte[] CopyData()
    {
        return _data.ToArray();
    }

    /// <summary>
    /// Append a byte array to the end of the packet. This may change the data
    /// buffer of this packet.
    /// </summary>
    /// <param name="data">Byte array to append</param>
    /// <param name="len">The number of bytes to append</param>
    public void Append(byte[] data, int len)
    {
        if (data == null || len <= 0 || len > data.Length)
        {
            throw new Exception("Invalid combination of parameters data and length to append()");
        }

        var oldLimit = _data.Length;
        // grow buffer if necessary
        Grow(len);
        data.AsSpan(0,len).CopyTo(_buffer.AsSpan(oldLimit, len));
    }

    /**
         * Get buffer containing the content of this packet
         *
         * @return buffer containing the content of this packet
         */
    public Memory<byte> GetBuffer()
    {
        return _data;
    }

    /**
         * Returns <tt>true</tt> if the extension bit of this packet has been set
         * and <tt>false</tt> otherwise.
         *
         * @return  <tt>true</tt> if the extension bit of this packet has been set
         * and <tt>false</tt> otherwise.
         */
    private bool GetExtensionBit()
    {
        return (_buffer[0] & 0x10) == 0x10;
    }

    /**
         * Returns the length of the extensions currently added to this packet.
         *
         * @return the length of the extensions currently added to this packet.
         */
    private ushort GetExtensionLength()
    {
        ushort length = 0;
        if (GetExtensionBit())
        {
            // the extension length comes after the RTP header, the CSRC list,
            // and after two bytes in the extension header called "defined by profile"
            var extLenIndex = FIXED_HEADER_SIZE + GetCsrcCount() * 4 + 2;
            length = BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(extLenIndex));
        }
        return length;
    }

    /**
         * Returns the number of CSRC identifiers currently included in this packet.
         *
         * @return the CSRC count for this <tt>RawPacket</tt>.
         */
    private int GetCsrcCount()
    {
        return _data.Span[0] & 0x0f;
    }

    /**
         * Get RTP header length from a RTP packet
         *
         * @return RTP header length from source RTP packet
         */
    public int GetHeaderLength()
    {
        var length = FIXED_HEADER_SIZE + 4 * GetCsrcCount();
        if (GetExtensionBit())
        {
            length += EXT_HEADER_SIZE + GetExtensionLength();
        }
        return length;
    }

    /**
         * Get the length of this packet's data
         *
         * @return length of this packet's data
         */
    public int GetLength()
    {
        return _data.Length;
    }

    /**
         * Get RTP payload length from a RTP packet
         *
         * @return RTP payload length from source RTP packet
         */
    public int GetPayloadLength()
    {
        return GetLength() - GetHeaderLength();
    }

    /**
         * Get RTCP SSRC from a RTCP packet
         *
         * @return RTP SSRC from source RTP packet
         */
    public int GetRtcpssrc()
    {
        return BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(4));
    }

    /// <summary>
    /// Get RTP sequence number from a RTP packet
    /// </summary>
    /// <returns>RTP sequence num from source packet</returns>
    public int GetSequenceNumber()
    {
        return BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(2));
    }

    /// <summary>
    /// Get SRTCP sequence number from a SRTCP packet
    /// </summary>
    /// <param name="authTagLen">authentication tag length</param>
    /// <returns>SRTCP sequence num from source packet</returns>
    public int GetSrtcpIndex(int authTagLen)
    {
        var offset = GetLength() - (4 + authTagLen);
        return BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(offset));
    }

    /// <summary>
    /// Get RTP SSRC from a RTP packet
    /// </summary>
    /// <returns>RTP SSRC from source RTP packet</returns>
    public int GetSsrc()
    {
        return BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(8));
    }

    /// <summary>
    /// Read a byte region from specified offset in the RTP packet and with
    /// specified length into a given buffer
    /// </summary>
    /// <param name="off">start offset in the RTP packet of the region to be read</param>
    /// <param name="len">length of the region to be read</param>
    /// <param name="outBuff">output buffer</param>
    public void ReadRegionToBuff(int off, int len, byte[] outBuff)
    {
        _buffer.AsSpan(off, len).CopyTo(outBuff);
    }

    /// <summary>
    /// Shrink the buffer of this packet by specified length
    /// </summary>
    /// <param name="delta">length to shrink</param>
    public void Shrink(int delta)
    {
        if (delta <= 0)
        {
            return;
        }

        var newLimit = _data.Length - delta;
        if (newLimit <= 0)
        {
            newLimit = 0;
        }

        _data = _buffer.AsMemory(0, newLimit);
    }

    /// <summary>
    /// Grow the internal packet buffer.
    ///
    /// This will change the data buffer of this packet but not the
    /// length of the valid data. Use this to grow the internal buffer
    /// to avoid buffer re-allocations when appending data.
    /// </summary>
    /// <param name="delta">number of bytes to grow</param>
    private void Grow(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var newLen = _data.Length + delta;
        if (newLen <= _buffer.Length)
        {
            // there is more room in the underlying reserved buffer memory
            _data = _buffer.AsMemory(0, newLen);
            return;
        }

        // create a new bigger buffer
        var newBuffer = new byte[newLen];
        _data.CopyTo(newBuffer);
        _buffer = newBuffer;
        _data = _buffer.AsMemory(0, newLen);
    }
}