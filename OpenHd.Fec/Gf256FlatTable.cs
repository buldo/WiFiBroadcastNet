using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace OpenHd.Fec;

public static class Gf256FlatTable
{
    static byte[,] mult = Tables.MOEPGF256_MUL_TABLE;
    static byte[][] tl = Tables.MOEPGF256_SHUFFLE_LOW_TABLE;
    static byte[][] th = Tables.MOEPGF256_SHUFFLE_HIGH_TABLE;

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

    public static void maddrc256_shuffle_ssse3(Span<byte> region1, Span<byte> region2, byte constant)
    {
        //assert(length % 16 == 0);
        //uint8_t* end;
        //__m128i t1, t2, m1, m2, in1, in2, out, l, h;

        if (constant == 0)
        {
            return;
        }

        if (constant == 1)
        {
            xorr_sse2(region1, region2);
            return;
        }

        var t1 = Vector128.Create(tl[constant]);
        var t2 = Vector128.Create(th[constant]);
        var m1 = Vector128.Create<byte>(0x0f);
        var m2 = Vector128.Create<byte>(0xf0);

        var iterationsCount = region1.Length / 16;

        for (int i = 0; i < iterationsCount; i++)
        {
            var reg1 = region1.Slice(i * 16, 16);
            var reg2 = region2.Slice(i * 16, 16);

            var in2 = Vector128.Create<byte>(reg2);                // in2 = _mm_loadu_si128((const __m128i *) region2);
            var in1 = Vector128.Create<byte>(reg1);                // in1 = _mm_loadu_si128((const __m128i *) region1);
            var l = Sse2.And(in2, m1);                             // l = _mm_and_si128(in2, m1);
            l = Ssse3.Shuffle(t1, l);                              // l = _mm_shuffle_epi8(t1, l);
            var h = Sse2.And(in2, m2);                             // h = _mm_and_si128(in2, m2);
            h = Sse2.ShiftRightLogical(h.AsUInt64(), 4).AsByte();  // h = _mm_srli_epi64(h, 4); //TODO: why not _mm_bsrli_si128?
            h = Ssse3.Shuffle(t2, h);                              // h = _mm_shuffle_epi8(t2, h);
            var outVar = Sse2.Xor(h, l);                           // out = _mm_xor_si128(h, l);
            outVar = Sse2.Xor(outVar, in1);                        // out = _mm_xor_si128(out, in1);
            outVar.CopyTo(reg1);                                   // _mm_storeu_si128((__m128i *) region1, out);
        }
    }

    private static void xorr_sse2(Span<byte> region1, Span<byte> region2)
    {
        var iterationsCount = region1.Length / 16;

        for (int i = 0; i < iterationsCount; i++)
        {
            var reg1 = region1.Slice(i * 16, 16);
            var reg2 = region2.Slice(i * 16, 16);
            var in2 = Vector128.Create<byte>(reg2);
            var in1 = Vector128.Create<byte>(reg1);
            Sse2
                .Xor(in1, in2)
                .CopyTo(reg1);
        }


        //assert(length % 16 == 0);
        //uint8_t* end;
        //__m128i in, out;

        //for (end = region1 + length; region1<end; region1 += 16, region2 += 16) {
        //    in = _mm_loadu_si128((const __m128i*) region2);
        //    out = _mm_loadu_si128((const __m128i*) region1);
        //    out = _mm_xor_si128(in, out);
        //    _mm_storeu_si128((__m128i*) region1, out);
        //}
    }
}