using System.Runtime.Intrinsics.X86;

namespace OpenHd.Fec;

public static class Gf256Optimized
{
    public static byte gf256_inverse(int value)
    {
        return Tables.MOEPGF256_INV_TABLE[value];
    }

    public static void gf256_madd_optimized(Span<byte> dst, Span<byte> src, byte c)
    {
        if (Sse3.IsSupported)
        {
            int sizeSlow = dst.Length % 16;
            int sizeFast = dst.Length - sizeSlow;
            if (sizeFast > 0)
            {
                Gf256FlatTable.maddrc256_shuffle_ssse3(dst, src, c);
            }
            if (sizeSlow > 0)
            {
                Gf256FlatTable.maddrc256_flat_table(dst.Slice(sizeFast, sizeFast), src.Slice(sizeFast, sizeFast), c);
            }
        }

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
        Gf256FlatTable.maddrc256_flat_table(dst, src, c);
        //#endif
    }

    public static byte gf256_mul(byte x, byte y)
    {
        return Gf256FlatTable.mulrc256_flat_table(0, x, y);
    }

    public static void gf256_mul_optimized(Span<byte> dst, Span<byte> src, byte c)
    {
        //#ifdef FEC_GF256_USE_X86_SSSE3
        //        const int sizeSlow = sz % 16;
        //        const int sizeFast = sz - sizeSlow;
        //        if(sizeFast>0){
        //            mulrc256_shuffle_ssse3(dst, src, c, sizeFast);
        //        }
        //        if(sizeSlow>0){
        //            mulrc256_flat_table(&dst[sizeFast],&src[sizeFast], c, sizeSlow);
        //        }
        //#elif defined(FEC_GF256_USE_ARM_NEON)
        //        const int sizeSlow = sz % 8;
        //        const int sizeFast = sz - sizeSlow;
        //        if (sizeFast > 0)
        //        {
        //            mulrc256_shuffle_neon_64(dst, src, c, sizeFast);
        //        }
        //        if (sizeSlow > 0)
        //        {
        //            mulrc256_flat_table(&dst[sizeFast], &src[sizeFast], c, sizeSlow);
        //        }
        //#else
        Gf256FlatTable.mulrc256_flat_table(dst, src, c);
        //#endif
    }
}