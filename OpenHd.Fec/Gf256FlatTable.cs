namespace OpenHd.Fec;

public static class Gf256FlatTable
{
    static byte[,] mult = Tables.MOEPGF256_MUL_TABLE;

    //public static void mulrc256_flat_table(Span<byte> region1, ReadOnlySpan<byte> region2, byte constant, int length)
    //{
    //    if (constant == 0)
    //    {
    //        for (int i = 0; i < length; i++)
    //        {
    //            region1[i] = 0;
    //        }
    //    }

    //    if (constant == 1)
    //    {
    //        for (int j = 0; j < length; j++)
    //        {
    //            region1[j] = region2[j];
    //        }
    //        return;
    //    }

    //    //for (; length != 0; region1++, region2++, length--)
    //    //{
    //    //    *region1 = mult[constant][*region2];
    //    //}
    //    int k = 0;
    //    for (; length != 0; length--)
    //    {
    //        region1[k] = mult[constant, region2[k]];
    //        k++;
    //    }
    //}

    public static unsafe void mulrc256_flat_table(byte* region1, byte* region2, byte constant, nint length)
    {
        if (constant == 0)
        {
            MemUtils.memset(region1, 0, length);
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

    public static unsafe void xorr_scalar(byte* region1, byte* region2, nint length)
    {
        for (; length != 0; region1++, region2++, length--)
        {
            *region1 ^= *region2;
        }
    }
}