namespace OpenHd.Fec;

public static class Gf256Optimized
{
    public static byte gf256_inverse(int value)
    {
        return Tables.MOEPGF256_INV_TABLE[value];
    }

    /// <summary>
    /// computes dst[] = dst[] + c * src[] where '+', '*' are gf256 operations
    /// </summary>
    public static unsafe void gf256_madd_optimized(byte* dst, byte* src, byte c, int sz)
    {
        //#ifdef FEC_GF256_USE_X86_SSSE3
        //        const int sizeSlow = sz % 16;
        //        const int sizeFast = sz - sizeSlow;
        //        if(sizeFast>0){
        //            maddrc256_shuffle_ssse3(dst, src, c, sizeFast);
        //        }
        //        if(sizeSlow>0){
        //            maddrc256_flat_table(&dst[sizeFast],&src[sizeFast], c, sizeSlow);
        //        }
        //        //maddrc256_flat_table(dst,src,c,sz);
        //#elif defined(FEC_GF256_USE_ARM_NEON)
        //        const int sizeSlow = sz % 8;
        //        const int sizeFast = sz - sizeSlow;
        //        if (sizeFast > 0)
        //        {
        //            maddrc256_shuffle_neon_64(dst, src, c, sizeFast);
        //        }
        //        if (sizeSlow > 0)
        //        {
        //            maddrc256_flat_table(&dst[sizeFast], &src[sizeFast], c, sizeSlow);
        //        }
        //#else
        Gf256FlatTable.maddrc256_flat_table(dst, src, c, sz);
        //#endif
    }

    public static unsafe byte gf256_mul(byte x, byte y)
    {
        byte ret;
        Gf256FlatTable.mulrc256_flat_table(&ret, &x, y, 1);
        return ret;
    }
}