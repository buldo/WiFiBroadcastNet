namespace OpenHd.Fec;

public static class Gf256FlatTable
{
    static byte[,] mult = Tables.MOEPGF256_MUL_TABLE;

    public static unsafe void mulrc256_flat_table(byte* region1, byte* region2, byte constant, nint length)
    {
        if (constant == 0)
        {
            MemUtils.memset(region1, 0, length);
            // TODO: Looks like here have to be return
        }

        if (constant == 1)
        {
            MemUtils.memcpy(region1, region2, length);
            return;
        }

        for (; length != 0; region1++, region2++, length--)
        {
            *region1 = mult[constant, *region2];
        }
    }

    public static void mulrc256_flat_table(Span<byte> region1, Span<byte> region2, byte constant)
    {
        if (constant == 0)
        {
            region1.Fill(0);
            // TODO: Looks like here have to be return
        }

        if (constant == 1)
        {
            region2.CopyTo(region1);
            return;
        }

        for (int i = 0; i < region1.Length; i++)
        {
            region1[i] = mult[constant, region2[i]];
        }
    }

    public static unsafe void maddrc256_flat_table(byte* region1, byte* region2, byte constant, nint length)
    {
        if (constant == 0)
            return;

        if (constant == 1)
        {
            xorr_scalar(region1, region2, length);
            return;
        }

        for (; length != 0; region1++, region2++, length--)
        {
            *region1 ^= mult[constant, *region2];
        }
    }

    public static void maddrc256_flat_table(Span<byte> region1, Span<byte> region2, byte constant)
    {
        if (constant == 0)
            return;

        if (constant == 1)
        {
            xorr_scalar(region1, region2);
            return;
        }

        for (int i = 0; i < region1.Length; i++)
        {
            region1[i] ^= mult[constant, region2[i]];
        }
    }

    private static unsafe void xorr_scalar(byte* region1, byte* region2, nint length)
    {
        for (; length != 0; region1++, region2++, length--)
        {
            *region1 ^= *region2;
        }
    }

    private static void xorr_scalar(Span<byte> region1, Span<byte> region2)
    {
        for (int i = 0; i < region1.Length; i++)
        {
            region1[i] ^= region2[i];
        }
    }
}