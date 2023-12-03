using System.Runtime.Intrinsics.Arm;
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
        if (Ssse3.IsSupported)
        {
            int sizeSlow = dst.Length % 16;
            int sizeFast = dst.Length - sizeSlow;
            if (sizeFast > 0)
            {
                Gf256FlatTable.maddrc256_shuffle_ssse3(dst.Slice(0, sizeFast), src.Slice(0, sizeFast), c);
            }
            if (sizeSlow > 0)
            {
                Gf256FlatTable.maddrc256_flat_table(dst.Slice(sizeFast, sizeSlow), src.Slice(sizeFast, sizeSlow), c);
            }
        }
        else if (AdvSimd.IsSupported)
        {
            int sizeSlow = dst.Length % 8;
            int sizeFast = dst.Length - sizeSlow;
            if (sizeFast > 0)
            {
                Gf256FlatTable.maddrc256_shuffle_neon_64(dst.Slice(0, sizeFast), src.Slice(0, sizeFast), c);
            }
            if (sizeSlow > 0)
            {
                Gf256FlatTable.maddrc256_flat_table(dst.Slice(sizeFast, sizeSlow), src.Slice(sizeFast, sizeSlow), c);
            }
        }
        else
        {
            Gf256FlatTable.maddrc256_flat_table(dst, src, c);
        }
    }

    public static byte gf256_mul(byte x, byte y)
    {
        return Gf256FlatTable.mulrc256_flat_table(0, x, y);
    }

    public static void gf256_mul_optimized(Span<byte> dst, Span<byte> src, byte c)
    {
        if (Ssse3.IsSupported)
        {
            int sizeSlow = dst.Length % 16;
            int sizeFast = dst.Length - sizeSlow;
            if (sizeFast > 0)
            {
                Gf256FlatTable.mulrc256_shuffle_ssse3(dst.Slice(0, sizeFast), src.Slice(0, sizeFast), c);
            }
            if (sizeSlow > 0)
            {
                Gf256FlatTable.mulrc256_flat_table(dst.Slice(sizeFast, sizeSlow), src.Slice(sizeFast, sizeSlow), c);
            }
        }
        else if (AdvSimd.IsSupported)
        {
            int sizeSlow = dst.Length % 8;
            int sizeFast = dst.Length - sizeSlow;
            if (sizeFast > 0)
            {
                Gf256FlatTable.mulrc256_shuffle_neon_64(dst.Slice(0, sizeFast), src.Slice(0, sizeFast), c);
            }
            if (sizeSlow > 0)
            {
                Gf256FlatTable.mulrc256_flat_table(dst.Slice(sizeFast, sizeSlow), src.Slice(sizeFast, sizeSlow), c);
            }
        }
        else
        {
            Gf256FlatTable.mulrc256_flat_table(dst, src, c);
        }
    }
}