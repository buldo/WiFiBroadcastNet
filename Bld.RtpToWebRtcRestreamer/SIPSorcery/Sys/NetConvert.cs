//-----------------------------------------------------------------------------
// Filename: Utilities.cs
//
// Description: Useful functions for VoIP protocol implementation.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 23 May 2005	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

public static class NetConvert
{
    public static ushort DoReverseEndian(ushort x)
    {
        //return Convert.ToUInt16((x << 8 & 0xff00) | (x >> 8));
        return BitConverter.ToUInt16(BitConverter.GetBytes(x).Reverse().ToArray(), 0);
    }

    public static uint DoReverseEndian(uint x)
    {
        //return (x << 24 | (x & 0xff00) << 8 | (x & 0xff0000) >> 8 | x >> 24);
        return BitConverter.ToUInt32(BitConverter.GetBytes(x).Reverse().ToArray(), 0);
    }

    /// <summary>
    /// Parse a UInt16 from a network buffer using network byte order.
    /// </summary>
    /// <param name="buffer">The buffer to parse the value from.</param>
    /// <param name="posn">The position in the buffer to start the parse from.</param>
    /// <returns>A UInt16 value.</returns>
    public static ushort ParseUInt16(byte[] buffer, int posn)
    {
        return (ushort)(buffer[posn] << 8 | buffer[posn + 1]);
    }

    /// <summary>
    /// Get a buffer representing the 64 bit unsigned integer in network
    /// byte (big endian) order.
    /// </summary>
    /// <param name="val">The value to convert.</param>
    /// <returns>A buffer representing the value in network order </returns>
    public static byte[] GetBytes(ulong val)
    {
        var buffer = new byte[8];
        BinaryPrimitives.TryWriteUInt64BigEndian(buffer, val);
        return buffer;
    }
}