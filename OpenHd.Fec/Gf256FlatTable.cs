namespace OpenHd.Fec;

public static class Gf256FlatTable
{
    static byte[,] mult = Tables.MOEPGF256_MUL_TABLE;

    public static byte mulrc256_flat_table(byte region1, byte region2, byte constant)
    {
        if (constant == 0)
        {
            return 0;
        }

        if (constant == 1)
        {
            return region2;
        }

        return mult[constant, region2];
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

    private static void xorr_scalar(Span<byte> region1, Span<byte> region2)
    {
        for (int i = 0; i < region1.Length; i++)
        {
            region1[i] ^= region2[i];
        }
    }
}