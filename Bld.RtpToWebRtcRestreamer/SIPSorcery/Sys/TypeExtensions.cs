//-----------------------------------------------------------------------------
// Filename: TypeExtensions.cs
//
// Description: Helper methods.
//
// Author(s):
// Aaron Clauson
//
// History:
// ??	Aaron Clauson	Created.
// 21 Jan 2020  Aaron Clauson   Added HexStr and ParseHexStr (borrowed from
//                              Bitcoin Core source).
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

public static class TypeExtensions
{
    private static readonly char[] Hexmap = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    public static string HexStr(this ReadOnlySpan<byte> buffer)
    {
        return Convert.ToHexString(buffer);
    }
    public static string HexStr(this Span<byte> buffer)
    {
        return Convert.ToHexString(buffer);
    }

    public static string HexStr(this Span<byte> buffer, char separator)
    {
        return buffer.HexStr(buffer.Length, separator);
    }

    private static string HexStr(this Span<byte> buffer, int length, char separator)
    {
        var rv = string.Empty;

        for (var i = 0; i < length; i++)
        {
            var val = buffer[i];
            rv += Hexmap[val >> 4];
            rv += Hexmap[val & 15];

            if (i != length - 1)
            {
                rv += separator;
            }
        }

        return rv;
    }

    /// <summary>
    /// Purpose of this extension is to allow deconstruction of a list into a fixed size tuple.
    /// </summary>
    /// <example>
    /// (var field0, var field1, var field2) = "a b c".Split();
    /// </example>
    // ReSharper disable once OutParameterValueIsAlwaysDiscarded.Global
    public static void Deconstruct<T>(this IList<T> list, out T first, out T second, out T third)
    {
        first = list.Count > 0 ? list[0] : default;
        second = list.Count > 1 ? list[1] : default;
        third = list.Count > 2 ? list[2] : default;
    }
}