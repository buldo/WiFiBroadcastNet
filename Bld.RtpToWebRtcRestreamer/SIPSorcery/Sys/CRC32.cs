// from http://damieng.com/blog/2006/08/08/Calculating_CRC32_in_C_and_NET

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

public static class Crc32
{
    private const uint DefaultPolynomial = 0xedb88320;
    private const uint DefaultSeed = 0xffffffff;

    private static uint[] _defaultTable;


    public static uint Compute(byte[] buffer)
    {
        return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
    }

    private static uint[] InitializeTable(uint polynomial)
    {
        if (polynomial == DefaultPolynomial && _defaultTable != null)
        {
            return _defaultTable;
        }

        var createTable = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ polynomial;
                }
                else
                {
                    entry = entry >> 1;
                }
            }
            createTable[i] = entry;
        }

        if (polynomial == DefaultPolynomial)
        {
            _defaultTable = createTable;
        }

        return createTable;
    }

    private static uint CalculateHash(uint[] table, uint seed, byte[] buffer, int start, int size)
    {
        var crc = seed;
        for (var i = start; i < size; i++)
        {
            unchecked
            {
                crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
            }
        }
        return crc;
    }
}